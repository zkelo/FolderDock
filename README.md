# FolderDock

Папки на панели задач Windows 11 — как в Dock на macOS. Клик по закреплённому значку папки → всплывающее окно с содержимым (сетка или список) прямо над панелью задач, без Проводника.

## Возможности

- **Закрепление любой папки** на панели задач через ярлык с собственным AppUserModelID — каждая папка получает отдельную кнопку, они не группируются между собой.
- **Popup над панелью задач**: сетка с миниатюрами (изображения — превью) или компактный список. Переключение кнопкой в заголовке.
- Клик по файлу → открытие приложением по умолчанию, popup закрывается.
- Клик по подпапке → навигация внутрь прямо в popup.
- **Типизированный drag-and-drop**: пакет данных содержит несколько представлений — картинки дополнительно кладутся как Bitmap (вставка «как картинка» в Paint/Word/мессенджеры), текстовые файлы — как Text (вставка содержимого в редактор), и всегда StorageItems (файлы для Проводника и всего остального). Приложение-приёмник само выбирает формат, который понимает.
- **Контекстное меню** (ПКМ по элементу): Открыть, Копировать (в буфер с теми же типизированными форматами: Ctrl+V в Paint вставит картинку, в блокноте — текст, в Проводнике — файл), Поделиться (системная панель Share Windows — почта, Bluetooth, OneDrive, мессенджеры), Показать в Проводнике.
- Кнопка «Открыть в Проводнике».
- Клик мимо окна → popup закрывается (поведение флайаута).
- Повторный клик по значку → toggle (закрыть).
- Тёмная/светлая тема — системная. Скруглённые углы Win11, поверх окон, нет в Alt-Tab.
- Single-instance: повторные запуски перенаправляются работающему экземпляру (AppInstance redirect).

## Сборка (на Windows)

Требования:
- Windows 10 17763+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 с workload **Windows application development** — или только SDK + командная строка

Командная строка:

```powershell
cd FolderDock
dotnet build -c Release -p:Platform=x64
# или сразу готовый exe (self-contained, WindowsAppSDKSelfContained — MSIX не нужен):
dotnet publish -c Release -r win-x64 -p:Platform=x64
```

Результат: `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\FolderDock.exe`

> WinUI 3 не собирается на Linux/macOS (XamlCompiler.exe — только Windows). Собирать на Windows.

## Использование

1. Запусти `FolderDock.exe` без аргументов → окно-менеджер.
2. «Добавить папку» → выбери папку → создаётся ярлык в `%LOCALAPPDATA%\FolderDock\Shortcuts` и открывается Проводник с ним.
3. ПКМ по ярлыку → «Показать дополнительные параметры» → **«Закрепить на панели задач»**.
4. Клик по закреплённому значку → popup с содержимым папки.

Так можно закрепить сколько угодно папок — у каждой свой значок.

## Как это работает

- Windows 11 не даёт закрепить папку на панель задач напрямую и не имеет API для popup «из панели». Обход: ярлык на `FolderDock.exe --folder "<путь>"` с записанным в property store ярлыка `System.AppUserModel.ID`, уникальным для каждой папки (SHA1 от пути). Процесс при старте ставит себе тот же AUMID через `SetCurrentProcessExplicitAppUserModelID` — панель задач сопоставляет окно с нужным закреплённым значком.
- Один фоновый процесс: `AppInstance.FindOrRegisterForKey` + `RedirectActivationToAsync`. Клик по любому ярлыку попадает в живой экземпляр → мгновенный popup без холодного старта.
- Popup — WinUI 3 `Window` c `OverlappedPresenter.CreateForContextMenu()` (без рамки), `IsAlwaysOnTop`, `WS_EX_TOOLWINDOW` (нет в Alt-Tab), `DWMWA_WINDOW_CORNER_PREFERENCE=ROUND`. Позиционирование — по курсору, прижат к верхнему краю рабочей области над панелью задач.
- Миниатюры — `StorageFile.GetThumbnailAsync` (те же превью, что в Проводнике), грузятся асинхронно после показа окна.

## Структура

| Файл | Назначение |
|---|---|
| `Program.cs` | Точка входа: single-instance, redirect активаций |
| `CommandLine.cs` | Разбор `--folder` из аргументов |
| `App.xaml(.cs)` | Роутинг активаций: popup или менеджер |
| `PopupWindow.xaml(.cs)` | Всплывающее окно: события UI, toggle, Share |
| `PopupChrome.cs` | Оформление окна: presenter, DWM, позиционирование, Share interop |
| `FolderEntry.cs` | Модель элемента папки |
| `FolderReader.cs` | Чтение содержимого папки |
| `ThumbnailLoader.cs` | Асинхронные миниатюры Проводника |
| `TypedDataPackage.cs` | DataPackage: Bitmap/Text/StorageItems по типу файла |
| `ManagerWindow.xaml(.cs)` | Менеджер: добавление папок, создание ярлыков |
| `ShortcutFactory.cs` | COM IShellLink + IPropertyStore: .lnk с AUMID |
| `FolderAumid.cs` | AppUserModelID из пути папки |
| `Shell.cs` | Запуск файлов и Проводника |
| `Native.cs` | Win32/COM: DWM, курсор, DPI, стили окна, Share interop |

## Ограничения

- Закрепление на панель — вручную через ПКМ (программный pin Windows не разрешает сторонним приложениям).
- Первый клик после перезагрузки — холодный старт (~1–2 с), дальше мгновенно.
- Иконка ярлыка — стандартная папка из shell32; можно сменить в свойствах ярлыка.
