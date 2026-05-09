using System;
using System.Linq;
using System.Security.Cryptography;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 16 / Bug #8 + Improvement #17 — тесты на парольную политику,
    /// журнал попыток входа, lockout, AES-шифрование Document.Summary,
    /// аудит расширенных действий (export/download), репозитории
    /// EmployeePasswordHistory / LoginAttempt / OrganizationSettings.
    /// </summary>
    public class Phase16SecurityAdminTests
    {
        private readonly IPasswordHasher _hasher = new Pbkdf2PasswordHasher(iterations: 1000);

        // ──────────────────────────────────────────────────────────────────────
        // Парольная политика.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void PasswordPolicy_default_min_length_rejects_short_passwords()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var policy = new PasswordPolicy(settings);

            var errors = policy.ValidateStrength("Ab1");

            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("8"));
        }

        [Fact]
        public void PasswordPolicy_requires_digit_uppercase_lowercase()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var policy = new PasswordPolicy(settings);

            var noDigit = policy.ValidateStrength("Password");
            var noUpper = policy.ValidateStrength("password1");
            var noLower = policy.ValidateStrength("PASSWORD1");

            Assert.Contains(noDigit, e => e.IndexOf("цифр", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Contains(noUpper, e => e.IndexOf("заглавн", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Contains(noLower, e => e.IndexOf("строчн", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void PasswordPolicy_accepts_compliant_password()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var policy = new PasswordPolicy(settings);

            var errors = policy.ValidateStrength("Secret123");

            Assert.Empty(errors);
        }

        [Fact]
        public void PasswordPolicy_history_check_blocks_reused_password()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var policy = new PasswordPolicy(settings);

            // Симулируем 3 предыдущих пароля.
            var history = new[]
            {
                new EmployeePasswordHistory { PasswordHash = _hasher.Hash("OldPass1A"), SetAt = DateTime.UtcNow.AddDays(-30) },
                new EmployeePasswordHistory { PasswordHash = _hasher.Hash("OldPass2B"), SetAt = DateTime.UtcNow.AddDays(-60) },
                new EmployeePasswordHistory { PasswordHash = _hasher.Hash("OldPass3C"), SetAt = DateTime.UtcNow.AddDays(-90) },
            };

            Assert.False(policy.ValidateAgainstHistory("OldPass2B", history, _hasher));
            Assert.True(policy.ValidateAgainstHistory("BrandNew9X", history, _hasher));
        }

        [Fact]
        public void PasswordPolicy_expiry_after_90_days_default()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var policy = new PasswordPolicy(settings);
            var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.False(policy.IsPasswordExpired(now.AddDays(-30), now));
            Assert.False(policy.IsPasswordExpired(now.AddDays(-89), now));
            Assert.True(policy.IsPasswordExpired(now.AddDays(-91), now));
            Assert.False(policy.IsPasswordExpired(null, now));
        }

        [Fact]
        public void PasswordPolicy_uses_organization_settings_overrides()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var s = settings.Get();
            s.PasswordMinLength = 12;
            s.PasswordExpiryDays = 30;
            settings.Save(s);

            var policy = new PasswordPolicy(settings);
            Assert.NotEmpty(policy.ValidateStrength("Short9X"));

            var now = DateTime.UtcNow;
            Assert.True(policy.IsPasswordExpired(now.AddDays(-31), now));
            Assert.False(policy.IsPasswordExpired(now.AddDays(-29), now));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lockout логика в AuthService.
        // ──────────────────────────────────────────────────────────────────────

        private (AuthService auth,
                 InMemoryEmployeeRepository employees,
                 InMemoryLoginAttemptRepository attempts,
                 AuditService audit,
                 InMemoryAuditLogRepository auditRepo,
                 InMemoryOrganizationSettingsRepository settings,
                 Employee alice) BuildAuth(Func<DateTime> now = null)
        {
            var employees = new InMemoryEmployeeRepository();
            var alice = new Employee
            {
                Id = 1,
                FullName = "Иванова Алиса",
                Role = EmployeeRole.Manager,
                IsActive = true,
                PasswordHash = _hasher.Hash("Secret123"),
                LastPasswordChangeAt = DateTime.UtcNow,
            };
            employees.Add(alice);

            var attempts = new InMemoryLoginAttemptRepository();
            var history = new InMemoryEmployeePasswordHistoryRepository();
            var settings = new InMemoryOrganizationSettingsRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var audit = new AuditService(auditRepo);
            var policy = new PasswordPolicy(settings);

            var auth = new AuthService(employees, _hasher,
                attempts, history, policy, settings, audit,
                now: now ?? (() => DateTime.UtcNow),
                ipProvider: () => "10.0.0.1");

            return (auth, employees, attempts, audit, auditRepo, settings, alice);
        }

        [Fact]
        public void AuthService_records_successful_login_attempt_and_audit()
        {
            var (auth, _, attempts, _, auditRepo, _, alice) = BuildAuth();

            Assert.True(auth.TryLogin("Иванова Алиса", "Secret123"));

            var allAttempts = attempts.Query(employeeId: alice.Id, fromUtc: null, toUtc: null, limit: 100);
            Assert.Single(allAttempts);
            Assert.True(allAttempts[0].Success);
            Assert.Equal("10.0.0.1", allAttempts[0].IpAddress);

            Assert.Contains(auditRepo.ListAllOrdered(), a => a.ActionType == AuditActionType.UserLogin);
        }

        [Fact]
        public void AuthService_records_failed_login_attempt_with_reason()
        {
            var (auth, _, attempts, _, auditRepo, _, alice) = BuildAuth();

            Assert.False(auth.TryLogin("Иванова Алиса", "wrong"));

            var allAttempts = attempts.Query(employeeId: alice.Id, fromUtc: null, toUtc: null, limit: 100);
            Assert.Single(allAttempts);
            Assert.False(allAttempts[0].Success);
            Assert.Equal(LoginFailureReason.WrongPassword, allAttempts[0].FailureReason);
            Assert.Contains(auditRepo.ListAllOrdered(), a => a.ActionType == AuditActionType.LoginAttemptFailed);
        }

        [Fact]
        public void AuthService_locks_account_after_5_failures_in_window()
        {
            var fixedNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var (auth, employees, attempts, _, auditRepo, _, alice) = BuildAuth(now: () => fixedNow);

            for (int i = 0; i < 5; i++)
            {
                auth.TryLogin("Иванова Алиса", "wrong");
            }

            var locked = employees.GetById(alice.Id);
            Assert.NotNull(locked.LockedUntil);
            Assert.True(locked.LockedUntil.Value > fixedNow);
            // 30-минутная блокировка по умолчанию.
            Assert.Equal(30, (int)Math.Round((locked.LockedUntil.Value - fixedNow).TotalMinutes));

            Assert.Contains(auditRepo.ListAllOrdered(), a => a.ActionType == AuditActionType.AccountLocked);
        }

        [Fact]
        public void AuthService_rejects_login_while_account_locked_even_with_correct_password()
        {
            var fixedNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var (auth, employees, _, _, _, _, alice) = BuildAuth(now: () => fixedNow);

            alice.LockedUntil = fixedNow.AddMinutes(15);
            employees.Save(alice);

            Assert.False(auth.TryLogin("Иванова Алиса", "Secret123"));
            Assert.Equal(LoginFailureReason.AccountLocked, auth.LastFailureReason);
        }

        [Fact]
        public void AuthService_rejects_login_for_inactive_employee()
        {
            var (auth, employees, _, _, _, _, alice) = BuildAuth();
            alice.IsActive = false;
            employees.Save(alice);

            Assert.False(auth.TryLogin("Иванова Алиса", "Secret123"));
            Assert.Equal(LoginFailureReason.AccountInactive, auth.LastFailureReason);
        }

        [Fact]
        public void AuthService_rejects_login_when_password_expired()
        {
            var fixedNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var (auth, employees, _, _, auditRepo, _, alice) = BuildAuth(now: () => fixedNow);
            alice.LastPasswordChangeAt = fixedNow.AddDays(-100);
            employees.Save(alice);

            Assert.False(auth.TryLogin("Иванова Алиса", "Secret123"));
            Assert.Equal(LoginFailureReason.PasswordExpired, auth.LastFailureReason);
            Assert.Contains(auditRepo.ListAllOrdered(), a => a.ActionType == AuditActionType.PasswordExpired);
        }

        [Fact]
        public void AuthService_clears_expired_lockout_on_successful_login()
        {
            var fixedNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var (auth, employees, _, _, _, _, alice) = BuildAuth(now: () => fixedNow);
            alice.LockedUntil = fixedNow.AddMinutes(-5); // истёкшая блокировка.
            employees.Save(alice);

            Assert.True(auth.TryLogin("Иванова Алиса", "Secret123"));
            Assert.Null(employees.GetById(alice.Id).LockedUntil);
        }

        // ──────────────────────────────────────────────────────────────────────
        // AES-шифрование Document.Summary.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Encryptor_returns_plaintext_when_key_not_configured()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var enc = new AesDocumentEncryptor(settings);

            Assert.False(enc.IsEnabled);
            Assert.Equal("Конфиденциально", enc.Encrypt("Конфиденциально"));
        }

        [Fact]
        public void Encryptor_round_trip_preserves_unicode_payload_after_rotation()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var enc = new AesDocumentEncryptor(settings);
            enc.RotateKey();

            const string plain = "Сводка по делу №2026-001 (конфиденциально, ДСП)";
            var cipher = enc.Encrypt(plain);

            Assert.True(enc.IsEnabled);
            Assert.NotEqual(plain, cipher);
            Assert.True(enc.IsEncryptedPayload(cipher));
            Assert.StartsWith("enc:v1:", cipher);
            Assert.Equal(plain, enc.Decrypt(cipher));
        }

        [Fact]
        public void Encryptor_detects_tampered_ciphertext_via_hmac()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var enc = new AesDocumentEncryptor(settings);
            enc.RotateKey();

            var cipher = enc.Encrypt("payload");
            // Меняем последний символ base64 — это «портит» MAC или cipher.
            var tampered = cipher.Substring(0, cipher.Length - 2)
                + (cipher[cipher.Length - 1] == 'A' ? "B" : "A") + "=";

            Assert.Throws<CryptographicException>(() => enc.Decrypt(tampered));
        }

        [Fact]
        public void Encryptor_passes_through_non_encrypted_payload_on_decrypt()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var enc = new AesDocumentEncryptor(settings);
            enc.RotateKey();

            // Plaintext без префикса не должен пытаться декодироваться —
            // это поддержка миграции старых записей до Phase 16.
            Assert.Equal("legacy plaintext", enc.Decrypt("legacy plaintext"));
        }

        [Fact]
        public void Encryptor_rotateKey_persists_new_key_and_timestamp()
        {
            var settings = new InMemoryOrganizationSettingsRepository();
            var enc = new AesDocumentEncryptor(settings);

            var key1 = enc.RotateKey();
            var t1 = settings.Get().EncryptionKeyGeneratedAt;
            var key2 = enc.RotateKey();
            var t2 = settings.Get().EncryptionKeyGeneratedAt;

            Assert.NotEqual(key1, key2);
            Assert.NotNull(t1);
            Assert.NotNull(t2);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Репозитории.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void LoginAttemptRepository_count_recent_failures_filters_by_window()
        {
            var attempts = new InMemoryLoginAttemptRepository();
            var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < 4; i++)
                attempts.Add(new LoginAttempt { EmployeeId = 1, Success = false, Timestamp = t0.AddMinutes(-i) });
            attempts.Add(new LoginAttempt { EmployeeId = 1, Success = true, Timestamp = t0 }); // не считается
            attempts.Add(new LoginAttempt { EmployeeId = 1, Success = false, Timestamp = t0.AddMinutes(-30) }); // вне окна

            int count = attempts.CountRecentFailures(employeeId: 1, fromUtc: t0.AddMinutes(-10));

            Assert.Equal(4, count);
        }

        [Fact]
        public void EmployeePasswordHistoryRepository_trim_keeps_n_newest()
        {
            var repo = new InMemoryEmployeePasswordHistoryRepository();
            var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 10; i++)
            {
                repo.Add(new EmployeePasswordHistory
                {
                    EmployeeId = 1,
                    PasswordHash = "hash" + i,
                    SetAt = t0.AddDays(-i),
                });
            }

            repo.TrimToDepth(employeeId: 1, depth: 3);
            var remaining = repo.ListForEmployee(1);

            Assert.Equal(3, remaining.Count);
            // Самые свежие 3 должны остаться (i=0..2 → SetAt = t0, t0-1d, t0-2d).
            Assert.Contains(remaining, e => e.PasswordHash == "hash0");
            Assert.Contains(remaining, e => e.PasswordHash == "hash1");
            Assert.Contains(remaining, e => e.PasswordHash == "hash2");
        }

        [Fact]
        public void OrganizationSettingsRepository_returns_defaults_on_first_get()
        {
            var repo = new InMemoryOrganizationSettingsRepository();
            var s = repo.Get();

            Assert.Equal(8, s.PasswordMinLength);
            Assert.Equal(90, s.PasswordExpiryDays);
            Assert.Equal(5, s.PasswordHistoryDepth);
            Assert.Equal(5, s.LockoutFailureThreshold);
            Assert.Equal(10, s.LockoutWindowMinutes);
            Assert.Equal(30, s.LockoutDurationMinutes);
        }

        [Fact]
        public void OrganizationSettingsRepository_save_round_trip()
        {
            var repo = new InMemoryOrganizationSettingsRepository();
            var s = repo.Get();
            s.PasswordMinLength = 12;
            s.LockoutDurationMinutes = 60;
            repo.Save(s);

            var reloaded = repo.Get();
            Assert.Equal(12, reloaded.PasswordMinLength);
            Assert.Equal(60, reloaded.LockoutDurationMinutes);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Audit hooks.
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void AttachmentService_download_emits_DocumentDownloaded_audit()
        {
            // Этот тест исполняется на in-memory компонентах; storage и repository
            // реализованы как простые подставные типы, чтобы изолировать audit.
            var auditRepo = new InMemoryAuditLogRepository();
            var audit = new AuditService(auditRepo);
            var docs = new InMemoryDocumentRepository();
            var doc = new Document
            {
                Title = "Test",
                Type = DocumentType.Office,
                CreationDate = DateTime.UtcNow,
                Deadline = DateTime.UtcNow.AddDays(7),
                Status = DocumentStatus.New,
            };
            docs.Add(doc);

            var attRepo = new InMemoryAttachmentRepository();
            var att = attRepo.Add(new DocumentAttachment
            {
                DocumentId = doc.Id,
                FileName = "report.pdf",
                StoragePath = "store/report.pdf",
                VersionNumber = 1,
                IsCurrentVersion = true,
                UploadedAt = DateTime.UtcNow,
                UploadedById = 1,
                Hash = "abc",
                FileType = AttachmentKind.Signed,
                SizeBytes = 100,
            });
            att.AttachmentGroupId = att.Id;
            attRepo.Update(att);

            var storage = new StubFileStorage();
            var svc = new AttachmentService(attRepo, docs, storage, audit);

            using (svc.Download(att.Id, downloadedById: 42)) { }

            Assert.Contains(auditRepo.ListAllOrdered(), a =>
                a.ActionType == AuditActionType.DocumentDownloaded
                && a.UserId == 42
                && a.EntityId == doc.Id);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Подставные типы для тестов.
        // ──────────────────────────────────────────────────────────────────────

        private sealed class StubFileStorage : IFileStorageService
        {
            public string Store(System.IO.Stream content, string registrationNumber, int version, string fileName)
                => $"stub/{registrationNumber}/v{version}/{fileName}";

            public System.IO.Stream Open(string storagePath) => new System.IO.MemoryStream(new byte[] { 1, 2, 3 });

            public bool Delete(string storagePath) => true;

            public bool Exists(string storagePath) => true;
        }
    }
}
