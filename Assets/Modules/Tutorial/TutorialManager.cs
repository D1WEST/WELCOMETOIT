using System.Linq;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.Perks;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Modules.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        private float _checkCooldown = 10f;
        private float _timer = 0f;
        private bool _firstTimeIntro = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            // 1. ГЛОБАЛЬНАЯ ПРОВЕРКА: Если Босс заткнут — вообще ничего не считаем
            if (!GameDataManager.Instance.bossHintsEnabled) return;

            _timer += Time.deltaTime;
            if (_timer < _checkCooldown) return;

            RunTutorialLogic();
            _timer = 0f;
        }

        private void RunTutorialLogic()
        {
            // Проверка внутри логики (на всякий случай)
            if (!GameDataManager.Instance.bossHintsEnabled) return;

            if (!_firstTimeIntro)
            {
                BossMessageUI.Instance.ShowHint("Добро пожаловать! Сначала найми людей в компьютере, потом рассади их.").Forget();
                _firstTimeIntro = true;
                return;
            }

            if (!ShiftManager.Instance.IsShiftActive)
            {
                if (WorkplaceInteractable.AllDesks.Any(d => d.HasWorker))
                {
                    BossMessageUI.Instance.ShowHint("Люди на местах! Жми на кнопку старта, пора делать деньги!").Forget();
                }
                return;
            }

            // Проверка света
            var breaker = FindFirstObjectByType<BreakerSwitch>();
            if (breaker != null && !breaker.isOn)
            {
                BossMessageUI.Instance.ShowHint("ТЕМНО КАК В МОГИЛЕ! Беги к рубильнику и зажми [E]!").Forget();
                return;
            }

            // Проверка мониторов
            foreach (var desk in WorkplaceInteractable.AllDesks)
            {
                if (desk.HasWorker && !desk.hasMonitor)
                {
                    BossMessageUI.Instance.ShowHint("Монитор на полу? Ставь его обратно, пока я не вычел его стоимость из твоей зарплаты!").Forget();
                    return;
                }
            }
        }

        // --- МЕТОДЫ-ТРИГГЕРЫ С ПРОВЕРКОЙ ---

        public void OnComputerOpened()
        {
            if (!GameDataManager.Instance.bossHintsEnabled) return;
            BossMessageUI.Instance.ShowHint("Опять в магазине? Бери самых дешевых, мне нужна прибыль, а не таланты!").Forget();
        }

        public void OnWorkplaceOpened()
        {
            if (!GameDataManager.Instance.bossHintsEnabled) return;
            BossMessageUI.Instance.ShowHint("Рассаживай их быстрее! Стулья не должны пустовать!").Forget();
        }

        public void OnPerkShopOpened()
        {
            if (!GameDataManager.Instance.bossHintsEnabled) return;
            BossMessageUI.Instance.ShowHint("Улучшения? Это будет стоить тебе дорого. Выбирай с умом!").Forget();
        }
    }
}