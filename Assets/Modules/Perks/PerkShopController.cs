using Assets.Modules.PlayerModule;
using Assets.Modules.Save;
using Assets.Modules.Audio; // Добавь этот неймспейс
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Assets.Modules.Perks
{
    public class PerkShopController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        private VisualElement _root;
        private VisualElement _list;
        private PlayerInput _cachedPlayer;

        private void Awake()
        {
            _root = uiDocument.rootVisualElement;
            _list = _root.Q<VisualElement>("perks-list");

            _root.Q<VisualElement>("overlay").style.display = DisplayStyle.None;

            var closeBtn = _root.Q<Button>("btn-close");
            if (closeBtn != null)
            {
                closeBtn.clicked += () => {
                    // ЗВУК КЛИКА (Индекс 6)
                    PlayClickSound();
                    Close();
                };
            }
        }

        public void Open(GameObject player)
        {
            _root.Q<VisualElement>("overlay").style.display = DisplayStyle.Flex;

            _cachedPlayer = player.GetComponent<PlayerInput>();
            if (_cachedPlayer != null) _cachedPlayer.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // ЗВУК ОТКРЫТИЯ (Клик, Индекс 6)
            PlayClickSound();

            RefreshUI();
        }

        public void Close()
        {
            _root.Q<VisualElement>("overlay").style.display = DisplayStyle.None;
            if (_cachedPlayer != null) _cachedPlayer.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
        }

        private void PlayClickSound()
        {
            // Используем индекс 6 для любых кликов по UI Босса
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Boss").ByIndex(6).RandomSound()
            ).Forget();
        }

        public void RefreshUI()
        {
            _list.Clear();
            var perks = GameDataManager.Instance.playerPerks;

            AddPerkRow("Хлесткий шлепок", "Снимает больше сна при ударе", perks.slapLevel, () => perks.slapLevel++);
            AddPerkRow("Чуткий нос", "Лисы нападают реже", perks.noseLevel, () => perks.noseLevel++);
            AddPerkRow("Золотая жила", "Больше очков прогресса за работу", perks.goldMineLevel, () => perks.goldMineLevel++);
            AddPerkRow("Вкусная поручка", "Премия снижает гнев и сон", perks.tastyBonusLevel, () => perks.tastyBonusLevel++);
        }

        private void AddPerkRow(string name, string desc, int level, System.Action onUpgrade)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingBottom = 10;
            row.style.marginBottom = 10;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(1, 1, 1, 0.1f);

            var leftGroup = new VisualElement();
            var nameLabel = new Label($"{name} ({level}/5)");
            nameLabel.style.color = Color.white;
            nameLabel.style.fontSize = 18;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var descLabel = new Label(desc);
            descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            descLabel.style.fontSize = 12;

            leftGroup.Add(nameLabel);
            leftGroup.Add(descLabel);

            var btn = new Button();
            int price = GameDataManager.Instance.GetPerkPrice(level);

            if (price == -1)
            {
                btn.text = "MAX";
                btn.SetEnabled(false);
                btn.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            }
            else
            {
                btn.text = $"UP ${price}";
                btn.style.backgroundColor = (GameDataManager.Instance.playerMoney >= price)
                    ? new Color(0.25f, 0.6f, 0.25f)
                    : new Color(0.4f, 0.2f, 0.2f);

                btn.clicked += () => {
                    if (GameDataManager.Instance.playerMoney >= price)
                    {
                        // ЗВУК ПОКУПКИ (Индекс 5)
                        AudioManager.Instance.PlayAudio(
                            AudioQuery.ByKey("Boss").ByIndex(5).RandomSound()
                        ).Forget();

                        GameDataManager.Instance.ChangeMoney(-price);
                        onUpgrade();
                        RefreshUI();
                    }
                    else
                    {
                        // Если денег нет, можно тоже проиграть звук ошибки или обычный клик
                        PlayClickSound();
                    }
                };
            }

            btn.style.width = 120;
            btn.style.height = 60;
            btn.style.color = Color.white;
            btn.style.fontSize = 14;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;

            row.Add(leftGroup);
            row.Add(btn);
            _list.Add(row);
        }
    }
}