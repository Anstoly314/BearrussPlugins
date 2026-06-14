# Bearruss Plugins

Репозиторий плагинов для Rust PvE сервера «Русский Медведь».

Авторская метка: **Bearruss Plugins**.

## Структура

```text
plugins/
  BearrussDamageControl/
    BearrussDamageControl.cs
  BearrussKitController/
    BearrussKitController.cs
  BearrussRateController/
    BearrussRateController.cs
configs/
docs/
```

## Модули

### BearrussDamageControl

Админский PvE/PvP damage-control модуль с GUI `/dm`.

Статус: базовая версия залита.

Функции:

- переключение режима PvE/PvP;
- категории урона;
- сохранение настроек в конфиг;
- защита PvE-сервера после глобальных обновлений Rust/uMod;
- исключения для дверей, ТС, ящиков, электрики, вагонов и NPC.

### BearrussKitController

Независимый контроллер китов.

Статус: первая версия залита.

Функции:

- `/kit` — GUI игрока;
- `/kitadmin` — GUI администратора;
- создание кита из инвентаря администратора;
- удаление китов через админ-GUI;
- фон GUI по ссылке из конфига;
- картинки китов только по ссылкам из конфига;
- permission для админки и отдельных китов;
- кулдауны и лимиты использования;
- выдача `supply.signal` новым игрокам первые 3 респавна.

### BearrussRateController

Контроллер рейтов, плавки и крафта.

Статус: файл создан локально, тестирование и финальная загрузка в GitHub позже.

План функций:

- день 08:00–20:00: x1 без привилегий;
- ночь 20:00–08:00: x5 без привилегий;
- чат-сообщения при смене рейтов;
- `bearrussratecontroller.viprate` — x5, плавка и крафт как ночью;
- `bearrussratecontroller.premrate` — x7, плавка и крафт 700%;
- русский конфиг;
- баланс-исключения: дизель максимум 4;
- оружие в ящиках, дропах и крейтах не умножается.

## Очередь разработки

1. Проверить компиляцию BearrussDamageControl.
2. Проверить компиляцию BearrussKitController.
3. Проверить BearrussRateController на сервере.
4. Перенести BearrussRateController в правильный путь:

```text
plugins/BearrussRateController/BearrussRateController.cs
```

5. Начать BearrussEconomyCore.
