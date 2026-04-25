using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class WorkerPhysical : MonoBehaviour, IInteractable
{
    private WorkerInstance _data;
    public WorkerInstance Data => _data;

    private float _sleepGrowthPerSec;
    private float _restlessGrowthPerSec;
    private float _angerGrowthPerSec;
    private bool _isKicking = false;

    public string InteractionPrompt
    {
        get
        {
            if (_data == null) return "";
            if (_isKicking) return "РАЗЪЯРЕН!";

            // Если шкала непоседливости выше 50 — приоритет на премию
            if (_data.currentRestlessness > 50) return $"Дать премию $200";

            return "Пнуть/Шлепнуть";
        }
    }

    public Transform InteractionPivot => transform;
    public InteractionType InteractionType => InteractionType.Click;
    public float HoldDuration => 0;

    public void Init(WorkerInstance data)
    {
        _data = data;
        CalculateSpeeds();
    }

    private void CalculateSpeeds()
    {
        if (_data == null) return;
        _sleepGrowthPerSec = CalculateStatSpeed(_data.sleepiness);
        _restlessGrowthPerSec = CalculateStatSpeed(_data.workPower);
        _angerGrowthPerSec = CalculateStatSpeed(_data.angriness);
    }

    private float CalculateStatSpeed(int statValue)
    {
        float hoursToFill = Mathf.Lerp(25f, 5f, (statValue - 1) / 9f);
        float secondsToFill = hoursToFill * 3600f;
        return 100f / secondsToFill;
    }

    private void Update()
    {
        if (_data == null || !ShiftManager.Instance.IsShiftActive || _data.isResting) return;

        float gameTimeStep = Time.deltaTime * ShiftManager.Instance.timeMultiplier;

        // 1. ОПРЕДЕЛЕНИЕ ТЕКУЩЕГО СТАТУСА (Логика "в обе стороны")
        if (_isKicking)
        {
            _data.status = WorkerStatus.Angry;
        }
        else if (_data.currentSleepiness >= 100)
        {
            _data.status = WorkerStatus.Sleeping;
        }
        else if (_data.currentRestlessness >= 100)
        {
            _data.status = WorkerStatus.Fidgeting;
        }
        else if (GetComponentInParent<WorkplaceInteractable>() != null && !GetComponentInParent<WorkplaceInteractable>().hasMonitor)
        {
            _data.status = WorkerStatus.NoEquipment;
        }
        else
        {
            // Если ни одно критическое условие не выполнено - он работает
            _data.status = WorkerStatus.Working;
        }

        // 2. ИЗМЕНЕНИЕ ШКАЛ В ЗАВИСИМОСТИ ОТ СТАТУСА
        if (_data.status == WorkerStatus.Working)
        {
            _data.currentSleepiness += _sleepGrowthPerSec * gameTimeStep;
            _data.currentRestlessness += _restlessGrowthPerSec * gameTimeStep;
            _data.currentAnger += (_angerGrowthPerSec * 0.3f) * gameTimeStep;
        }
        else if (_data.status == WorkerStatus.Sleeping)
        {
            // Во время сна сонливость падает быстрее
            _data.currentSleepiness -= (_sleepGrowthPerSec * 4f) * gameTimeStep;
            _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness);
        }
        else if (_data.status == WorkerStatus.Fidgeting || _data.status == WorkerStatus.NoEquipment)
        {
            // Если он не работает, он потихоньку "успокаивается" сам, но очень медленно
            _data.currentRestlessness -= (_restlessGrowthPerSec * 0.5f) * gameTimeStep;
            _data.currentRestlessness = Mathf.Max(0, _data.currentRestlessness);
        }

        // 3. ПРОВЕРКА НА ПИНОК МОНИТОРА
        if (_data.currentAnger >= 100 && !_isKicking)
        {
            PerformKick().Forget();
        }
    }

    private async UniTaskVoid PerformKick()
    {
        _isKicking = true;

        var desk = GetComponentInParent<WorkplaceInteractable>();
        if (desk != null && desk.hasMonitor)
        {
            Debug.Log($"{_data.name} ПИНАЕТ МОНИТОР!");
            await UniTask.Delay(800); // Время на замах
            desk.KickMonitor();
        }

        await UniTask.Delay(3000); // 3 секунды ярости
        _data.currentAnger = 40; // Гнев падает после разрядки
        _isKicking = false;
    }

    public void Interact(GameObject interactor)
    {
        if (_data == null || _isKicking) return;

        // Приоритет 1: Премия (от непоседливости)
        if (_data.currentRestlessness > 50)
        {
            if (GameDataManager.Instance.playerMoney >= 200)
            {
                GameDataManager.Instance.ChangeMoney(-200);
                _data.currentRestlessness = 0;
                Debug.Log($"Премия дана {_data.name}.");
            }
            return;
        }

        // Приоритет 2: Шлепок (от сна или просто так)
        PerformSlap();
    }

    private void PerformSlap()
    {
        _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness - 25f);
        _data.currentAnger = Mathf.Min(100, _data.currentAnger + 30f);
        Debug.Log("ШЛЕПОК!");
    }
}