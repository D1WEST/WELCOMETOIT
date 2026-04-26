using Assets.Modules.Interractables.Impl;
using Assets.Modules.Perks;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using System.Linq;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    private float _checkCooldown = 7f; // Босс ворчит не чаще чем раз в 7 секунд
    private float _timer = 0f;

    private bool _firstTimeIntro = false;

    private void Update()
    {
        // Обучение работает ТОЛЬКО на первом дне
        if (GameDataManager.Instance.loadedDay != 1) return;

        _timer += Time.deltaTime;
        if (_timer < _checkCooldown) return;

        RunTutorialLogic();
        _timer = 0f;
    }

    private void RunTutorialLogic()
    {
        // 1. ПРИВЕТСТВИЕ (самое начало игры)
        if (!_firstTimeIntro)
        {
            BossMessageUI.Instance.ShowHint("Добро пожаловать в OVERTASK! Сначала найми бездельников в компьютере, потом рассади их.").Forget();
            _firstTimeIntro = true;
            return;
        }

        // 2. ЕСЛИ СМЕНА ЕЩЕ НЕ НАЧАТА
        if (!ShiftManager.Instance.IsShiftActive)
        {
            // Проверяем, сидит ли кто-то за столом
            bool someoneAssigned = WorkplaceInteractable.AllDesks.Any(d => d.HasWorker);
            if (someoneAssigned)
            {
                BossMessageUI.Instance.ShowHint("Люди на местах! Подойди к кнопке и нажми [E], чтобы начать смену.").Forget();
            }
            return;
        }

        // --- ЛОГИКА ВО ВРЕМЯ СМЕНЫ ---

        // 3. ПРОВЕРКА ЭЛЕКТРИЧЕСТВА (Самый высокий приоритет)
        var breaker = FindFirstObjectByType<BreakerSwitch>();
        if (breaker != null && !breaker.isOn)
        {
            BossMessageUI.Instance.ShowHint("ТЫ ЧТО, ТЕМНОТЫ БОИШЬСЯ? Беги к рубильнику и ЗАЖМИ [E], чтобы включить свет!").Forget();
            return;
        }

        // 4. ПРОВЕРКА МОНИТОРОВ
        foreach (var desk in WorkplaceInteractable.AllDesks)
        {
            if (desk.HasWorker && !desk.hasMonitor)
            {
                BossMessageUI.Instance.ShowHint("Рабочий без монитора — это просто мебель! Подними монитор с пола и поставь на стол [E].").Forget();
                return;
            }
        }

        // 5. ПРОВЕРКА СПЯЩИХ
        foreach (var desk in WorkplaceInteractable.AllDesks)
        {
            if (desk.HasWorker && desk.Worker.status == Assets.Modules.NPC.WorkerStatus.Sleeping)
            {
                BossMessageUI.Instance.ShowHint($"{desk.Worker.name} ДРИХНЕТ! Подойди и дай ему хорошего пинка на [E]!").Forget();
                return;
            }
        }

        // 6. ПРОВЕРКА НЕПОСЕДЛИВЫХ (Скука)
        foreach (var desk in WorkplaceInteractable.AllDesks)
        {
            if (desk.HasWorker && desk.Worker.status == Assets.Modules.NPC.WorkerStatus.Fidgeting)
            {
                BossMessageUI.Instance.ShowHint($"{desk.Worker.name} отлынивает! Дай ему премию, чтобы он снова начал приносить бабки.").Forget();
                return;
            }
        }
    }
    public void OnComputerOpened()
    {
        if (!GameDataManager.Instance.bossHintsEnabled) return;
        BossMessageUI.Instance.ShowHint("Опять в магазине торчишь? Бери самых дешевых, нам не нужны таланты, нам нужны цифры!").Forget();
    }

    public void OnWorkplaceOpened()
    {
        if (!GameDataManager.Instance.bossHintsEnabled) return;
        BossMessageUI.Instance.ShowHint("Рассаживай их быстрее! Стулья не должны пустовать, это потерянная прибыль!").Forget();
    }

    public void OnPerkShopOpened()
    {
        if (!GameDataManager.Instance.bossHintsEnabled) return;
        BossMessageUI.Instance.ShowHint("Хочешь стать лучше? Это будет стоить тебе целое состояние. Выбирай быстрее!").Forget();
    }
}