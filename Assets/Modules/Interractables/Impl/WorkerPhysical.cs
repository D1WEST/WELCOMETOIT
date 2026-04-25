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
    private int _prem => 200 + (GameDataManager.Instance.loadedDay * 10);

    public string InteractionPrompt
    {
        get
        {
            if (_data == null) return "";
            if (_isKicking) return "РАЗЪЯРЕН!";

            // Если шкала непоседливости выше 50 — приоритет на премию
            if (_data.currentRestlessness > 50) return $"{_prem} $";

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

        if (_data.status == WorkerStatus.Working)
        {
            _data.currentSleepiness += _sleepGrowthPerSec * 4 * gameTimeStep;
            _data.currentRestlessness += _restlessGrowthPerSec * 4 * gameTimeStep;
            _data.currentAnger += (_angerGrowthPerSec + _restlessGrowthPerSec * 0.3f + _sleepGrowthPerSec * 0.3f) * 4 * gameTimeStep;
        }
        else if (_data.status == WorkerStatus.Sleeping)
        {
            _data.currentSleepiness -= (_sleepGrowthPerSec * 4f) * gameTimeStep;
            _data.currentAnger -= (_angerGrowthPerSec * 4f) * gameTimeStep;
        }
        else if (_data.status == WorkerStatus.Fidgeting)
        {
            _data.currentRestlessness -= (_restlessGrowthPerSec * 1.5f) * gameTimeStep;
            _data.currentAnger -= (_angerGrowthPerSec * 1.5f) * gameTimeStep;
        }

        _data.currentSleepiness = Mathf.Clamp(_data.currentSleepiness, 0, 100.1f);
        _data.currentRestlessness = Mathf.Clamp(_data.currentRestlessness, 0, 100.1f);

        if (_isKicking)
        {
            _data.status = WorkerStatus.Angry;
        }
        else if (_data.status == WorkerStatus.Sleeping)
        {
            if (_data.currentSleepiness <= 70f) _data.status = WorkerStatus.Working;
        }
        else if (_data.status == WorkerStatus.Fidgeting)
        {
            if (_data.currentRestlessness <= 70f) _data.status = WorkerStatus.Working;
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
            _data.status = WorkerStatus.Working;
        }

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
            await UniTask.Delay(800);
            desk.KickMonitor();
        }

        await UniTask.Delay(3000);
        _data.currentAnger = 10;
        _isKicking = false;
    }

    public void Interact(GameObject interactor)
    {
        if (_data == null || _isKicking) return;

        // ЛОГИКА ПРЕМИИ
        if (_data.currentRestlessness > 50)
        {
            if (GameDataManager.Instance.playerMoney >= _prem)
            {
                GameDataManager.Instance.ChangeMoney(-_prem);
                _data.currentRestlessness = 0;
                _data.status = WorkerStatus.Working;
                int tastyLevel = GameDataManager.Instance.playerPerks.tastyBonusLevel;
                if (tastyLevel > 0)
                {
                    _data.currentAnger = Mathf.Max(0, _data.currentAnger - (tastyLevel * 10f));
                    _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness - (tastyLevel * 10f));
                }
            }
            return;
        }

        PerformSlap();
    }

    private void PerformSlap()
    {
        float baseSlapPower = 25f;
        float perkBonus = baseSlapPower * (0.2f * GameDataManager.Instance.playerPerks.slapLevel);
        float finalSlapEffect = baseSlapPower + perkBonus;

        _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness - finalSlapEffect);
        _data.currentAnger = Mathf.Min(100, _data.currentAnger + 30f);
        _data.status = WorkerStatus.Working;
    }
}