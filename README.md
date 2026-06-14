# Bearruss Plugins

Репозиторий плагинов для Rust PvE сервера «Русский Медведь».

Авторская метка: **Bearruss Plugins**.

## Структура

```text
plugins/
  BearrussDamageControl/
    BearrussDamageControl.cs
    README.md
configs/
docs/
```

## Текущий модуль

### BearrussDamageControl

Админский PvE/PvP damage-control модуль с GUI `/dm`.

План:

- переключение режима PvE/PvP;
- категории урона;
- сохранение настроек в конфиг;
- защита PvE-сервера после глобальных обновлений Rust/uMod;
- исключения для дверей, ТС, ящиков, электрики, вагонов и NPC.
