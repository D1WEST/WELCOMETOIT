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

            // Если шкала непоседливости выше 50 — приоритет на премию
            if (_data.currentRestlessness > 50)
            {
                return $"Дать премию $200";
            }

            // В любом другом случае (спит он или просто работает) — опция шлепка
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
        // Рассчитываем скорость заполнения (на 1 игровую секунду)
        // Мапим стат 1-10 на время заполнения 2.5дня - 0.5дня
        _sleepGrowthPerSec = CalculateStatSpeed(_data.sleepiness);
        _restlessGrowthPerSec = CalculateStatSpeed(_data.workPower); // Непоседливость привяжем к силе/скорости
        _angerGrowthPerSec = CalculateStatSpeed(_data.angriness);
    }

    private float CalculateStatSpeed(int statValue)
    {
        float hoursToFill = Mathf.Lerp(25f, 5f, (statValue - 1) / 9f); // от 25 до 5 игровых часов
        float secondsToFill = hoursToFill * 3600f;
        return 100f / secondsToFill;
    }

    private async void Update()
    {

        if (_data == null || !ShiftManager.Instance.IsShiftActive || _data.isResting) return;

        float gameTimeStep = Time.deltaTime * ShiftManager.Instance.timeMultiplier;

        if (_data.status == WorkerStatus.Working)
        {
            _data.currentSleepiness += _sleepGrowthPerSec * gameTimeStep;
            _data.currentRestlessness += _restlessGrowthPerSec * gameTimeStep;
            _data.currentAnger += (_angerGrowthPerSec * 0.5f) * gameTimeStep; // Гнев растет медленнее при работе
        }

        if (_data.currentSleepiness >= 100) _data.status = WorkerStatus.Sleeping;
        if (_data.currentRestlessness >= 100) _data.status = WorkerStatus.Fidgeting;

        if (_data.status == WorkerStatus.Sleeping)
        {
            _data.currentSleepiness -= (_sleepGrowthPerSec * 2f) * gameTimeStep;
            if (_data.currentSleepiness <= 0) _data.status = WorkerStatus.Working;
        }

        if (_data.currentAnger >= 100 && !_isKicking)
        {
            await PerformKick();
        }
    }

    private async UniTask PerformKick()
    {
        _isKicking = true;
        _data.status = WorkerStatus.Angry;

        // Находим стол
        var desk = GetComponentInParent<WorkplaceInteractable>();
        if (desk != null && desk.hasMonitor)
        {
            Debug.Log($"{_data.name} ПИНАЕТ МОНИТОР!");
            await UniTask.Delay(1000);
            desk.KickMonitor();
        }

        await UniTask.Delay(4000);
        _data.currentAnger = 50;
        _isKicking = false;
    }

    public void Interact(GameObject interactor)
    {
        if (_data == null) return;
        if (_data.currentRestlessness > 50)
        {
            if (GameDataManager.Instance.playerMoney >= 200)
            {
                GameDataManager.Instance.ChangeMoney(-200);
                _data.currentRestlessness = 0;
                _data.status = WorkerStatus.Working;
                Debug.Log($"Премия дана {_data.name}. Он доволен.");
            }
            else
            {
                Debug.Log("Недостаточно денег!");
            }
            return;
        }

        PerformSlap();
    }
    private void PerformSlap()
    {
        _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness - 20f);

        _data.currentAnger = Mathf.Min(100, _data.currentAnger + 30f);

        _data.status = WorkerStatus.Working;
    }
}