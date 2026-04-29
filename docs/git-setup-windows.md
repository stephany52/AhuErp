# Установка и настройка Git для AhuErp на Windows

Эта инструкция нужна тебе один раз — поднять Git на твоём `DESKTOP-I1OTVEB`,
чтобы можно было клонировать `coappo/AhuErp`, забирать изменения, делать
ветки и коммиты прямо с Windows. Дальше Git пригодится не только для
миграций, но и для всей разработки на проекте.

---

## 1. Скачать и установить Git for Windows

1. Открой <https://git-scm.com/download/win> — установщик скачается автоматически
   (файл вида `Git-2.NN.N-64-bit.exe`).
2. Запусти установщик. Жми **Next** на каждом экране, кроме следующих, где
   стоит остановиться:

   | Экран                                  | Что выбрать                                                                              |
   |----------------------------------------|------------------------------------------------------------------------------------------|
   | *Select Components*                    | Оставь все галочки по умолчанию (включая *Git Bash Here*, *Git GUI Here*).               |
   | *Choosing the default editor*          | **Use Visual Studio Code as Git's default editor** (если у тебя есть VS Code) — иначе оставь Vim. |
   | *Adjusting your PATH environment*      | **Git from the command line and also from 3rd-party software** — обязательно эту опцию.  |
   | *Choosing HTTPS transport backend*     | **Use the OpenSSL library**.                                                             |
   | *Configuring the line ending conversions* | **Checkout Windows-style, commit Unix-style line endings** (`core.autocrlf=true`).    |
   | *Configuring the terminal emulator*    | **Use MinTTY** (нужно для нормального Git Bash).                                         |
   | *Choose the default behavior of git pull* | **Default (fast-forward or merge)**.                                                  |
   | *Choose a credential helper*           | **Git Credential Manager** — это важно, оно сохранит логин в GitHub после первого OAuth. |
   | *Configuring extra options*            | Оставь по умолчанию (file system caching ON, symbolic links OFF).                        |

3. На последнем экране сними галочку **View Release Notes**, нажми **Finish**.

4. Открой **новый** терминал (cmd, PowerShell или Git Bash) и проверь:

   ```cmd
   git --version
   ```

   Должна напечатать что-то вроде `git version 2.46.0.windows.1`.

---

## 2. Базовая настройка Git

В любом терминале выполни (подставь свои имя и почту — они попадут в каждый
коммит, желательно использовать ту же почту, что в GitHub):

```cmd
git config --global user.name  "Имя Фамилия"
git config --global user.email "ваш-github-email@example.com"

rem  Чтобы новые ветки создавались как main, а не master
git config --global init.defaultBranch main

rem  Включаем удобный pretty-вывод и поддержку UTF-8 (русские имена файлов)
git config --global core.quotepath false
git config --global i18n.commitencoding utf-8
git config --global i18n.logoutputencoding utf-8

rem  Длинные пути (для глубоких node_modules / EF6 packages)
git config --global core.longpaths true
```

Если установщик предложил, line endings уже настроены через `core.autocrlf=true`
— перепроверить можно так:

```cmd
git config --global --get core.autocrlf
```

Должно ответить `true`.

---

## 3. Авторизация на GitHub

Установщик включил **Git Credential Manager**. При первом `git push`
он откроет окно браузера → войди в GitHub под пользователем `coappo`
(или коллаборатором, у которого есть push-доступ к `coappo/AhuErp`)
→ разреши доступ. Дальше учётка кэшируется в Windows Credential Manager
и больше не спрашивается.

Если по каким-то причинам OAuth не сработал, можно по-старому через
Personal Access Token:

1. <https://github.com/settings/tokens> → **Generate new token (classic)**
2. Scopes: `repo` (минимально достаточно).
3. Сохрани токен (он показывается только один раз).
4. При первом push введи логин = `coappo`, пароль = токен.

---

## 4. Клонировать репозиторий

Открой папку, куда хочешь положить код, например `C:\src`:

```cmd
mkdir C:\src
cd C:\src
git clone https://github.com/coappo/AhuErp.git
cd AhuErp
```

После этого:

- `git status` — посмотреть, какие файлы изменены;
- `git log --oneline -20` — последние 20 коммитов;
- `git branch -a` — все ветки (локальные + удалённые `origin/...`).

---

## 5. Базовый ежедневный workflow

```cmd
rem  1. Подтянуть свежий main
git checkout main
git pull --ff-only origin main

rem  2. Сделать ветку под свою задачу
git checkout -b fix/название-задачи

rem  3. Поправить файлы (Visual Studio / VS Code) и посмотреть, что изменилось
git status
git diff

rem  4. Закоммитить нужное (можно несколько раз)
git add path\to\file1.cs path\to\file2.cs
git commit -m "fix: краткое описание изменения"

rem  5. Запушить ветку на GitHub
git push -u origin fix/название-задачи

rem  6. На GitHub откроется ссылка "Compare & pull request" - нажми её,
rem     заполни описание, создай PR.
```

> **Никогда не пушь напрямую в `main`.** Всегда создавай ветку и PR — даже
> если ты единственный коммитер. Это позволит Devin Review автоматически
> просмотреть PR и подсветить баги.

---

## 6. Что делать, если запутался

```cmd
rem  Откатить изменения в одном файле до последнего коммита
git checkout -- path\to\file.cs

rem  Снять файл со staging (но изменения сохранить)
git restore --staged path\to\file.cs

rem  Посмотреть, что сейчас в staging vs working tree
git diff --cached
git diff

rem  ОПАСНО: выкинуть ВСЕ незакоммиченные изменения
rem    git reset --hard HEAD   - использовать только если уверен
rem    git clean -fd           - удалит и неотслеживаемые файлы
git reset --hard HEAD
git clean -fd
```

Если не уверен — лучше сначала закоммитить временный коммит
(`git commit -am "wip"`), а потом разбираться: коммиты гораздо проще
откатить, чем вернуть случайно удалённые файлы.

---

## 7. После установки Git: возвращаемся к миграциям

1. Открой `Developer Command Prompt for VS 2022` (Start → Visual Studio →
   Developer Command Prompt). Это нужно, чтобы в `PATH` были `dotnet`
   и `sqlcmd`.
2. Перейди в корень репо: `cd C:\src\AhuErp`.
3. Запусти батник:

   ```cmd
   tools\regen-migrations.bat
   ```

   Если SQL-инстанс отличается от `DESKTOP-I1OTVEB\SQLEXPRESS`, передай
   аргументом:

   ```cmd
   tools\regen-migrations.bat "MY-PC\SQLEXPRESS"
   tools\regen-migrations.bat "MY-PC\SQLEXPRESS" AhuErpDb_MyScaffold
   ```

4. Дождись `=== Done ===`. В выводе будет путь к бекапу старых `.resx`
   (на случай, если что-то пойдёт не так — можно вернуть).
5. В Visual Studio открой решение, дождись восстановления NuGet.
6. Открой Package Manager Console (`Tools → NuGet Package Manager →
   Package Manager Console`) и проверь:

   ```powershell
   Add-Migration TestEmpty -ProjectName AhuErp.Core -StartUpProjectName AhuErp.UI
   ```

   - Если `Up()`/`Down()` пусты — модель и слепок синхронны. Удали тестовую
     миграцию: `Remove-Migration -ProjectName AhuErp.Core -StartUpProjectName AhuErp.UI`,
     закоммить обновлённый `202604300000000_AddSearchIndex.resx`, открой PR.
   - Если в `Up()` есть `CreateTable/AddColumn` — значит модель ушла дальше,
     дополни код миграции и снова прогони батник.

---

## 8. Полезные ссылки

- Документация Git: <https://git-scm.com/docs>
- Хороший интерактивный туториал: <https://learngitbranching.js.org/>
- Шпаргалка GitHub: <https://education.github.com/git-cheat-sheet-education.pdf>
- EF6 Code-First Migrations: <https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/migrations/>
