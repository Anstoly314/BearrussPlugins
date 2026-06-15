using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BearrussEconomyCore", "Bearruss Plugins", "1.0.0")]
    [Description("Core economy with HUD, admin commands, silent rewards and blood harvesting.")]
    public class BearrussEconomyCore : RustPlugin
    {
        private const string PermAdmin = "bearrusseconomycore.admin";
        private const string HudName = "BearrussEconomyCore.HUD";

        private PluginConfig _config;
        private StoredData _data;
        private readonly HashSet<uint> _bloodGiven = new HashSet<uint>();
        private readonly HashSet<int> _treeRewarded = new HashSet<int>();

        #region Config / Data

        private class PluginConfig
        {
            public CurrencySettings Валюта = new CurrencySettings();
            public HudSettings HUD = new HudSettings();
            public RewardSettings Начисления = new RewardSettings();
            public BloodSettings Кровь = new BloodSettings();
            public SaveSettings Сохранение = new SaveSettings();
        }

        private class CurrencySettings
        {
            public string Название = "рубли";
            public string Символ = "₽";
            public double Стартовый_баланс = 0;
        }

        private class HudSettings
        {
            public bool Включить_HUD = true;
            public string AnchorMin = "0.82 0.03";
            public string AnchorMax = "0.98 0.075";
            public string Цвет_фона = "0 0 0 0.35";
            public string Цвет_текста = "1 0.82 0.25 1";
            public int Размер_текста = 15;
            public float Интервал_обновления_секунд = 10f;
            public string Формат = "💰 {balance} ₽";
        }

        private class RewardSettings
        {
            public bool Включить_начисления = true;
            public int Онлайн_интервал_минут = 30;
            public double Онлайн_рублей = 15;
            public double Бочка_рублей = 2;
            public double Дерево_рублей = 1;
            public double Медведь_рублей = 5;
            public double NPC_рублей = 3;
        }

        private class BloodSettings
        {
            public bool Включить_кровь = true;
            public string Shortname_предмета_крови = "blood";
            public string DisplayName_крови = "Кровь";
            public ulong SkinId_крови = 0;
            public int Медведь_минимум = 1;
            public int Медведь_максимум = 12;
            public int Курица_минимум = 1;
            public int Курица_максимум = 1;
            public int Остальные_минимум = 7;
            public int Остальные_максимум = 10;
        }

        private class SaveSettings
        {
            public float Автосохранение_секунд = 300f;
        }

        private class StoredData
        {
            public Dictionary<ulong, PlayerData> Игроки = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            public string Имя = "";
            public double Баланс;
            public double Всего_заработано;
            public double Всего_потрачено;
            public double Последняя_онлайн_награда;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) throw new Exception("Config is null");
            }
            catch
            {
                PrintWarning("Конфиг поврежден. Создаю новый конфиг по умолчанию.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (_data == null)
                _data = new StoredData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);

            cmd.AddChatCommand("bal", this, nameof(CmdBalance));
            cmd.AddChatCommand("money", this, nameof(CmdBalance));

            cmd.AddChatCommand("ecoadd", this, nameof(CmdEcoAdd));
            cmd.AddChatCommand("ecotake", this, nameof(CmdEcoTake));
            cmd.AddChatCommand("ecoset", this, nameof(CmdEcoSet));
            cmd.AddChatCommand("ecoreset", this, nameof(CmdEcoReset));
            cmd.AddChatCommand("ecoaddall", this, nameof(CmdEcoAddAll));

            cmd.AddConsoleCommand("eco.add", this, nameof(ConsoleEcoAdd));
            cmd.AddConsoleCommand("eco.take", this, nameof(ConsoleEcoTake));
            cmd.AddConsoleCommand("eco.set", this, nameof(ConsoleEcoSet));
            cmd.AddConsoleCommand("eco.reset", this, nameof(ConsoleEcoReset));

            LoadData();
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
                EnsurePlayer(player);

            if (_config.HUD.Включить_HUD)
                timer.Every(_config.HUD.Интервал_обновления_секунд, RefreshAllHud);

            timer.Every(60f, OnlineRewardTick);
            timer.Every(_config.Сохранение.Автосохранение_секунд, SaveData);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyHud(player);

            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            EnsurePlayer(player);

            timer.Once(2f, () =>
            {
                if (player != null && player.IsConnected)
                    RefreshHud(player);
            });
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            DestroyHud(player);
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (!_config.Начисления.Включить_начисления || entity == null || info == null)
                return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId())
                return;

            if (IsBarrel(entity))
            {
                AddMoney(attacker.userID, _config.Начисления.Бочка_рублей, "Бочка", false);
                RefreshHud(attacker);
                return;
            }

            if (IsBear(entity))
            {
                AddMoney(attacker.userID, _config.Начисления.Медведь_рублей, "Медведь", false);
                RefreshHud(attacker);
                return;
            }

            if (IsNpc(entity))
            {
                AddMoney(attacker.userID, _config.Начисления.NPC_рублей, "NPC", false);
                RefreshHud(attacker);
            }
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!_config.Начисления.Включить_начисления || dispenser == null || player == null || item == null)
                return;

            if (!IsWood(item))
                return;

            var key = dispenser.GetHashCode();
            if (_treeRewarded.Contains(key))
                return;

            _treeRewarded.Add(key);
            AddMoney(player.userID, _config.Начисления.Дерево_рублей, "Дерево", false);
            RefreshHud(player);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!_config.Кровь.Включить_кровь || dispenser == null || entity == null || item == null)
                return;

            var player = entity as BasePlayer;
            if (player == null || !player.userID.IsSteamId())
                return;

            var source = dispenser.GetComponent<BaseEntity>();
            if (source == null)
                return;

            TryGiveBlood(player, source);
        }

        #endregion

        #region Chat Commands

        private void CmdBalance(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            player.ChatMessage($"Ваш баланс: {FormatMoney(GetBalance(player.userID))}");
        }

        private void CmdEcoAdd(BasePlayer player, string command, string[] args)
        {
            if (!HasAdmin(player)) return;
            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /ecoadd ник сумма");
                return;
            }

            var target = FindPlayer(args[0]);
            if (target == null)
            {
                player.ChatMessage("Игрок не найден.");
                return;
            }

            if (!TryParseAmount(args[1], out var amount))
            {
                player.ChatMessage("Некорректная сумма.");
                return;
            }

            AddMoney(target.userID, amount, $"Админская выдача: {player.displayName}", true);
            player.ChatMessage($"Выдано {FormatMoney(amount)} игроку {target.displayName}.");
        }

        private void CmdEcoTake(BasePlayer player, string command, string[] args)
        {
            if (!HasAdmin(player)) return;
            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /ecotake ник сумма");
                return;
            }

            var target = FindPlayer(args[0]);
            if (target == null)
            {
                player.ChatMessage("Игрок не найден.");
                return;
            }

            if (!TryParseAmount(args[1], out var amount))
            {
                player.ChatMessage("Некорректная сумма.");
                return;
            }

            TakeMoney(target.userID, amount, $"Админское списание: {player.displayName}", true);
            player.ChatMessage($"Списано {FormatMoney(amount)} у игрока {target.displayName}.");
        }

        private void CmdEcoSet(BasePlayer player, string command, string[] args)
        {
            if (!HasAdmin(player)) return;
            if (args.Length < 2)
            {
                player.ChatMessage("Использование: /ecoset ник сумма");
                return;
            }

            var target = FindPlayer(args[0]);
            if (target == null)
            {
                player.ChatMessage("Игрок не найден.");
                return;
            }

            if (!TryParseAmount(args[1], out var amount))
            {
                player.ChatMessage("Некорректная сумма.");
                return;
            }

            SetMoney(target.userID, amount, $"Админская установка: {player.displayName}", true);
            player.ChatMessage($"Баланс игрока {target.displayName} установлен: {FormatMoney(amount)}.");
        }

        private void CmdEcoReset(BasePlayer player, string command, string[] args)
        {
            if (!HasAdmin(player)) return;
            if (args.Length < 1)
            {
                player.ChatMessage("Использование: /ecoreset ник");
                return;
            }

            var target = FindPlayer(args[0]);
            if (target == null)
            {
                player.ChatMessage("Игрок не найден.");
                return;
            }

            SetMoney(target.userID, 0, $"Админский сброс: {player.displayName}", true);
            player.ChatMessage($"Баланс игрока {target.displayName} сброшен.");
        }

        private void CmdEcoAddAll(BasePlayer player, string command, string[] args)
        {
            if (!HasAdmin(player)) return;
            if (args.Length < 1)
            {
                player.ChatMessage("Использование: /ecoaddall сумма");
                return;
            }

            if (!TryParseAmount(args[0], out var amount))
            {
                player.ChatMessage("Некорректная сумма.");
                return;
            }

            var count = 0;
            foreach (var target in BasePlayer.activePlayerList)
            {
                if (target == null || !target.userID.IsSteamId()) continue;
                AddMoney(target.userID, amount, $"Массовая выдача: {player.displayName}", true);
                count++;
            }

            player.ChatMessage($"Выдано {FormatMoney(amount)} всем онлайн. Игроков: {count}.");
        }

        #endregion

        #region Console Commands

        private void ConsoleEcoAdd(ConsoleSystem.Arg arg)
        {
            if (!CanUseConsole(arg)) return;
            if (!TryGetConsoleArgs(arg, out var userId, out var amount)) return;
            AddMoney(userId, amount, "Console", true);
        }

        private void ConsoleEcoTake(ConsoleSystem.Arg arg)
        {
            if (!CanUseConsole(arg)) return;
            if (!TryGetConsoleArgs(arg, out var userId, out var amount)) return;
            TakeMoney(userId, amount, "Console", true);
        }

        private void ConsoleEcoSet(ConsoleSystem.Arg arg)
        {
            if (!CanUseConsole(arg)) return;
            if (!TryGetConsoleArgs(arg, out var userId, out var amount)) return;
            SetMoney(userId, amount, "Console", true);
        }

        private void ConsoleEcoReset(ConsoleSystem.Arg arg)
        {
            if (!CanUseConsole(arg)) return;
            var userId = arg.GetULong(0);
            if (userId == 0) return;
            SetMoney(userId, 0, "Console reset", true);
        }

        #endregion

        #region API

        private double GetBalance(ulong userId)
        {
            return GetPlayerData(userId).Баланс;
        }

        private bool HasMoney(ulong userId, double amount)
        {
            return GetBalance(userId) >= amount;
        }

        private bool AddMoney(ulong userId, double amount, string reason = "", bool refreshHud = true)
        {
            if (userId == 0 || amount <= 0) return false;

            var data = GetPlayerData(userId);
            data.Баланс += amount;
            data.Всего_заработано += amount;

            if (refreshHud)
                RefreshHud(userId);

            return true;
        }

        private bool TakeMoney(ulong userId, double amount, string reason = "", bool refreshHud = true)
        {
            if (userId == 0 || amount <= 0) return false;

            var data = GetPlayerData(userId);
            data.Баланс -= amount;
            if (data.Баланс < 0) data.Баланс = 0;
            data.Всего_потрачено += amount;

            if (refreshHud)
                RefreshHud(userId);

            return true;
        }

        private bool SetMoney(ulong userId, double amount, string reason = "", bool refreshHud = true)
        {
            if (userId == 0) return false;

            var data = GetPlayerData(userId);
            data.Баланс = Math.Max(0, amount);

            if (refreshHud)
                RefreshHud(userId);

            return true;
        }

        #endregion

        #region Rewards

        private void OnlineRewardTick()
        {
            if (!_config.Начисления.Включить_начисления) return;

            var interval = Math.Max(1, _config.Начисления.Онлайн_интервал_минут) * 60;
            var now = Now();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || !player.userID.IsSteamId()) continue;

                var data = GetPlayerData(player.userID);
                if (data.Последняя_онлайн_награда <= 0)
                {
                    data.Последняя_онлайн_награда = now;
                    continue;
                }

                if (now - data.Последняя_онлайн_награда >= interval)
                {
                    AddMoney(player.userID, _config.Начисления.Онлайн_рублей, "Онлайн", false);
                    data.Последняя_онлайн_награда = now;
                    RefreshHud(player);
                }
            }
        }

        private void TryGiveBlood(BasePlayer player, BaseEntity source)
        {
            if (player == null || source == null) return;

            var id = source.net != null ? source.net.ID.Value : 0;
            if (id != 0 && _bloodGiven.Contains(id)) return;

            var name = GetName(source);
            int min;
            int max;

            if (name.Contains("bear"))
            {
                min = _config.Кровь.Медведь_минимум;
                max = _config.Кровь.Медведь_максимум;
            }
            else if (name.Contains("chicken"))
            {
                min = _config.Кровь.Курица_минимум;
                max = _config.Кровь.Курица_максимум;
            }
            else if (IsAnimalName(name))
            {
                min = _config.Кровь.Остальные_минимум;
                max = _config.Кровь.Остальные_максимум;
            }
            else return;

            var amount = UnityEngine.Random.Range(Math.Min(min, max), Math.Max(min, max) + 1);
            if (amount <= 0) return;

            var item = ItemManager.CreateByName(_config.Кровь.Shortname_предмета_крови, amount, _config.Кровь.SkinId_крови);
            if (item == null)
            {
                PrintWarning($"Не найден предмет крови: {_config.Кровь.Shortname_предмета_крови}");
                return;
            }

            if (!string.IsNullOrEmpty(_config.Кровь.DisplayName_крови))
                item.name = _config.Кровь.DisplayName_крови;

            player.GiveItem(item);
            if (id != 0) _bloodGiven.Add(id);
        }

        #endregion

        #region HUD

        private void RefreshAllHud()
        {
            if (!_config.HUD.Включить_HUD) return;

            foreach (var player in BasePlayer.activePlayerList)
                RefreshHud(player);
        }

        private void RefreshHud(ulong userId)
        {
            var player = BasePlayer.FindByID(userId);
            if (player != null && player.IsConnected)
                RefreshHud(player);
        }

        private void RefreshHud(BasePlayer player)
        {
            if (player == null || !player.IsConnected || !_config.HUD.Включить_HUD) return;

            DestroyHud(player);

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = _config.HUD.Цвет_фона },
                RectTransform = { AnchorMin = _config.HUD.AnchorMin, AnchorMax = _config.HUD.AnchorMax },
                CursorEnabled = false
            }, "Overlay", HudName);

            var balance = GetBalance(player.userID);
            var text = _config.HUD.Формат.Replace("{balance}", Math.Floor(balance).ToString("0"));

            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = text,
                    FontSize = _config.HUD.Размер_текста,
                    Align = TextAnchor.MiddleCenter,
                    Color = _config.HUD.Цвет_текста
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, HudName);

            CuiHelper.AddUi(player, container);
        }

        private void DestroyHud(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, HudName);
        }

        #endregion

        #region Helpers

        private PlayerData GetPlayerData(ulong userId)
        {
            if (!_data.Игроки.ContainsKey(userId))
            {
                _data.Игроки[userId] = new PlayerData
                {
                    Баланс = _config.Валюта.Стартовый_баланс,
                    Последняя_онлайн_награда = Now()
                };
            }

            return _data.Игроки[userId];
        }

        private void EnsurePlayer(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            var data = GetPlayerData(player.userID);
            data.Имя = player.displayName;
        }

        private bool HasAdmin(BasePlayer player)
        {
            if (player == null) return false;
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermAdmin);
        }

        private bool CanUseConsole(ConsoleSystem.Arg arg)
        {
            if (arg == null) return false;
            var player = arg.Player();
            return player == null || HasAdmin(player);
        }

        private bool TryGetConsoleArgs(ConsoleSystem.Arg arg, out ulong userId, out double amount)
        {
            userId = arg.GetULong(0);
            amount = arg.GetDouble(1);
            return userId != 0 && amount > 0;
        }

        private BasePlayer FindPlayer(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            if (ulong.TryParse(text, out var id))
            {
                var byId = BasePlayer.FindByID(id) ?? BasePlayer.FindSleeping(id);
                if (byId != null) return byId;
            }

            text = text.ToLower();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName != null && player.displayName.ToLower().Contains(text))
                    return player;
            }

            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player.displayName != null && player.displayName.ToLower().Contains(text))
                    return player;
            }

            return null;
        }

        private bool TryParseAmount(string text, out double amount)
        {
            if (double.TryParse(text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out amount))
                return amount > 0;

            amount = 0;
            return false;
        }

        private string FormatMoney(double amount)
        {
            return $"{Math.Floor(amount):0} {_config.Валюта.Символ}";
        }

        private double Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private bool IsWood(Item item)
        {
            return item != null && item.info != null && item.info.shortname == "wood";
        }

        private bool IsBarrel(BaseEntity entity)
        {
            var name = GetName(entity);
            return name.Contains("barrel");
        }

        private bool IsBear(BaseEntity entity)
        {
            return GetName(entity).Contains("bear");
        }

        private bool IsNpc(BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player == null) return false;
            if (player.userID.IsSteamId()) return false;

            var name = GetName(entity);
            return player.IsNpc || name.Contains("scientist") || name.Contains("murderer") || name.Contains("bandit");
        }

        private bool IsAnimalName(string name)
        {
            return name.Contains("wolf") ||
                   name.Contains("boar") ||
                   name.Contains("stag") ||
                   name.Contains("deer") ||
                   name.Contains("horse") ||
                   name.Contains("chicken") ||
                   name.Contains("bear") ||
                   name.Contains("panther") ||
                   name.Contains("crocodile");
        }

        private string GetName(BaseEntity entity)
        {
            if (entity == null) return string.Empty;
            return (entity.ShortPrefabName ?? string.Empty).ToLower();
        }

        #endregion
    }
}
