# PlayerForGame

Музыкальный пульт для Windows: горячие клавиши, оверлей, перемотка и работа в фоне.

**Актуальная версия: 1.1.1**

[Скачать для Windows x64](https://github.com/ostapenko-Nik/Music.Release/releases/download/v1.1.1/PlayerForGame-v1.1.1-win-x64.zip) · [Описание релиза](https://github.com/ostapenko-Nik/Music.Release/releases/tag/v1.1.1)

## Установка и обновление

1. Завершите старую версию через **Exit** в меню трея.
2. Распакуйте весь ZIP в постоянную папку и запустите **VKMusicController.exe**. Среда .NET включена в архив.
3. Откройте музыку в браузере.
4. Для громкости только VK установите папку **BrowserExtension** как распакованное расширение Chrome/Edge. [Инструкция](src/VKMusicController/BrowserExtension/README.md).

Настройки сохраняются в `%APPDATA%\VKMusicController\settings.json` и остаются на месте при обновлении.

## Возможности

- Главное окно, настройки по вкладкам и три цветовые темы.
- Оверлей с обложкой, кнопками и перемоткой до нужной секунды.
- Новое уведомление о треке: плотный фон, скруглённая обложка, закрытие и открытие плеера по клику.
- Уведомления не перехватывают фокус у игры.
- При автозапуске главное окно и настройки скрыты; оверлей показывается согласно настройке.
- Двойной щелчок по значку в трее открывает главное окно.
- Громкость VK регулируется отдельно от системного звука через расширение.

Перемотка и получение данных трека зависят от возможностей медиасессии браузера.

## Горячие клавиши по умолчанию

| Комбинация | Действие |
| --- | --- |
| Ctrl + Alt + Space | Play / Pause |
| Ctrl + Alt + ← / → | Предыдущий / следующий трек |
| Ctrl + Alt + O | Показать / скрыть оверлей |
| Ctrl + Alt + ↑ / ↓ | Громкость VK + / − 5% |
| Ctrl + Alt + M | Выключить / вернуть звук VK |

Комбинации можно изменить в настройках.

## Скриншоты

Из готовой сборки, с демонстрационной обложкой.

![Уведомление](https://github.com/ostapenko-Nik/Music.Release/releases/download/v1.1.1/notification.png)

![Главное окно](https://github.com/ostapenko-Nik/Music.Release/releases/download/v1.1.1/main-menu.png)

## Сборка из исходников

Нужны Windows и .NET 8 SDK.

```powershell
dotnet run --project src/VKMusicController/VKMusicController.csproj -c Debug
dotnet publish src/VKMusicController/VKMusicController.csproj -c Release -r win-x64 --self-contained true -o src/VKMusicController/bin/Releases/v1.1.1/publish
```

## Проверки

```powershell
dotnet run --project tests/PlayerUi.Tests/PlayerUi.Tests.csproj
node --test tests/extension.test.cjs
dotnet run --project tests/VolumeBridge.Tests/VolumeBridge.Tests.csproj
```

Перед проверкой локального моста громкости завершите приложение: тест использует порт 43821.
UI-проверки покрывают перемотку, автозапуск, 64 комбинации компоновки оверлея и жизненный цикл уведомления.
Для проверки готовой сборки укажите абсолютный путь к опубликованной DLL:

```powershell
dotnet build tests/PlayerUi.Tests/PlayerUi.Tests.csproj -p:PlayerPublishedAssembly=C:/path/to/publish/VKMusicController.dll -o ui-check
dotnet ui-check/PlayerUi.Tests.dll
```
