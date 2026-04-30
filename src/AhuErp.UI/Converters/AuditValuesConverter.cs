using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace AhuErp.UI.Converters
{
    /// <summary>
    /// Phase 12 / A8 — конвертер строк OldValues/NewValues аудита: переводит
    /// технические ключи (key=value; …) в человекочитаемые подписи на русском.
    /// Если входной строки нет в карте — она возвращается без изменений.
    /// </summary>
    public sealed class AuditValuesConverter : IValueConverter
    {
        private static readonly IReadOnlyDictionary<string, string> KeyMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RegistrationNumber"] = "Регистрационный №",
                ["RegistrationDate"] = "Дата регистрации",
                ["CaseId"] = "Дело",
                ["DocumentId"] = "Документ",
                ["ExecutorId"] = "Исполнитель",
                ["AuthorId"] = "Автор",
                ["ControllerId"] = "Контролёр",
                ["Deadline"] = "Срок",
                ["From"] = "С кого",
                ["To"] = "На кого",
                ["Reason"] = "Причина",
                ["Status"] = "Статус",
                ["Kind"] = "Тип",
                ["Channel"] = "Канал",
                ["IsLocked"] = "Заблокирован",
                ["IsActive"] = "Активен",
                ["IsRevoked"] = "Отозвана",
                ["IsShared"] = "Общий",
                ["Name"] = "Название",
                ["Code"] = "Код",
                ["Year"] = "Год",
                ["Index"] = "Индекс",
                ["Title"] = "Заголовок",
                ["Original"] = "Замещаемый",
                ["Substitute"] = "Заместитель",
                ["AttachmentId"] = "Вложение",
                ["Length"] = "Длина",
                ["DelegationId"] = "Делегирование",
                ["HeadEmployeeId"] = "Руководитель",
                ["TaskId"] = "Поручение",
                ["BasisDocumentId"] = "Документ-основание",
                ["Item"] = "Позиция",
                ["Qty"] = "Количество",
                ["Vehicle"] = "Транспорт",
                ["Driver"] = "Водитель",
                ["TemplateId"] = "Маршрут",
                ["Decision"] = "Решение",
                ["ApprovalId"] = "Согласование",
                ["DocumentTypeRefId"] = "Вид документа",
                ["NomenclatureCaseId"] = "Дело",
                ["DefaultDirection"] = "Направление",
                ["DefaultRetentionYears"] = "Срок хранения",
                ["RegistrationNumberTemplate"] = "Шаблон номера",
                ["ShortCode"] = "Код",
                ["Direction"] = "Направление",
                ["AccessLevel"] = "Гриф доступа",
                ["AssignedEmployeeId"] = "Исполнитель",
                ["Comment"] = "Комментарий",
            };

        private static readonly IReadOnlyDictionary<string, string> ValueMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["true"] = "да",
                ["false"] = "нет",
                ["null"] = "—",
                ["Substitution"] = "По замещению",
                ["Deactivated"] = "Деактивировано",
                ["Pending"] = "Ожидает",
                ["Approved"] = "Согласовано",
                ["Rejected"] = "Отклонено",
                ["Comments"] = "С замечаниями",
                ["New"] = "Новый",
                ["InProgress"] = "В работе",
                ["Completed"] = "Завершён",
                ["Cancelled"] = "Отменён",
                ["Internal"] = "Внутренний",
                ["Incoming"] = "Входящий",
                ["Outgoing"] = "Исходящий",
                ["Directive"] = "Распорядительный",
            };

        private static readonly Regex Splitter = new Regex(@"\s*;\s*", RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;
            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text)) return text;

            var parts = Splitter.Split(text);
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(TranslatePart(parts[i]));
            }
            return sb.ToString();
        }

        private static string TranslatePart(string part)
        {
            if (string.IsNullOrEmpty(part)) return part;
            var idx = part.IndexOf('=');
            if (idx <= 0) return TranslateFreeText(part);

            var rawKey = part.Substring(0, idx).Trim();
            var rawVal = part.Substring(idx + 1).Trim();
            var key = KeyMap.TryGetValue(rawKey, out var k) ? k : rawKey;
            var val = ValueMap.TryGetValue(rawVal, out var v) ? v : rawVal;
            return key + " = " + val;
        }

        private static string TranslateFreeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (EntityMap.TryGetValue(text, out var entity)) return entity;
            if (ValueMap.TryGetValue(text, out var value)) return value;
            return text;
        }

        private static readonly IReadOnlyDictionary<string, string> EntityMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AttachmentTextIndices"] = "индекс вложений",
                ["Document"] = "документ",
                ["DocumentApproval"] = "согласование",
                ["DocumentAttachment"] = "вложение",
                ["DocumentResolution"] = "резолюция",
                ["DocumentSignature"] = "подпись",
                ["DocumentTask"] = "поручение",
                ["DocumentTypeRef"] = "вид документа",
                ["InventoryTransaction"] = "движение ТМЦ",
                ["NomenclatureCase"] = "дело номенклатуры",
                ["VehicleTrip"] = "путевой лист"
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
