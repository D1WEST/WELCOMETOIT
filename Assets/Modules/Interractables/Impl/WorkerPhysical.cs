using Assets.Modules.Audio;
using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class WorkerPhysical : MonoBehaviour, IInteractable
{
    [Header("Visuals")]
    private Animator _animator;
    private WorkerInstance _data;
    public WorkerInstance Data => _data;

    private float _sleepGrowthPerSec;
    private float _restlessGrowthPerSec;
    private float _angerGrowthPerSec;
    private bool _isKicking = false;
    private int _prem => 200 + (GameDataManager.Instance.loadedDay * 10);

    // ВАЖНО: Храним предыдущий статус, чтобы менять анимацию только при его смене
    private WorkerStatus _lastStatus;
    private bool _lastRestingState;

    public string InteractionPrompt
    {
        get
        {
            if (_data == null) return "";
            if (_isKicking) return "РАЗЪЯРЕН!";
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
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
            Debug.LogError($"[WorkerPhysical] Аниматор не найден в {gameObject.name}!");

        _lastStatus = data.status;
        _lastRestingState = data.isResting;

        CalculateSpeeds();
        UpdateAnimationState();
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
        if (_data == null || !ShiftManager.Instance.IsShiftActive) return;

        // 1. Проверка режима отдыха (из компьютера)
        if (_data.isResting != _lastRestingState)
        {
            _lastRestingState = _data.isResting;
            UpdateAnimationState();
        }

        if (_data.isResting) return;

        float gameTimeStep = Time.deltaTime * ShiftManager.Instance.timeMultiplier;

        // 2. Логика начисления статов и звуков (ваши оригинальные звуки)
        if (_data.status == WorkerStatus.Working)
        {
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Keyboard").WithVolume(1f).AsRandomPlaylist().AsKeyInstance().At(this.transform));
            _data.currentSleepiness += _sleepGrowthPerSec * 2 * gameTimeStep;
            _data.currentRestlessness += _restlessGrowthPerSec * 2 * gameTimeStep;
            _data.currentAnger += (_angerGrowthPerSec + _restlessGrowthPerSec * 0.3f + _sleepGrowthPerSec * 0.3f) * 2 * gameTimeStep;
        }
        else if (_data.status == WorkerStatus.Sleeping)
        {
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Emotion_Sleepy").WithVolume(0.1f).RandomSound().AsKeyInstance().At(this.transform));
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

        // 3. Расчет нового статуса (теперь через цепочку else-if для стабильности)
        WorkerStatus newStatus = _data.status;

        if (_isKicking)
        {
            newStatus = WorkerStatus.Angry;
        }
        else if (_data.status == WorkerStatus.Sleeping && _data.currentSleepiness > 70f)
        {
            newStatus = WorkerStatus.Sleeping;
        }
        else if (_data.status == WorkerStatus.Fidgeting && _data.currentRestlessness > 70f)
        {
            newStatus = WorkerStatus.Fidgeting;
        }
        else if (_data.currentSleepiness >= 100)
        {
            newStatus = WorkerStatus.Sleeping;
        }
        else if (_data.currentRestlessness >= 100)
        {
            newStatus = WorkerStatus.Fidgeting;
        }
        else if (GetComponentInParent<WorkplaceInteractable>() != null && !GetComponentInParent<WorkplaceInteractable>().hasMonitor)
        {
            newStatus = WorkerStatus.NoEquipment;
        }
        else
        {
            newStatus = WorkerStatus.Working;
        }

        // Применяем новый статус
        _data.status = newStatus;

        // 4. ГЛАВНЫЙ ФИКС: Обновляем анимацию ТУТ, только если статус РЕАЛЬНО изменился
        if (_data.status != _lastStatus)
        {
            UpdateAnimationState();
            _lastStatus = _data.status;
        }

        if (_data.currentAnger >= 100 && !_isKicking)
        {
            PerformKick().Forget();
        }
    }

    private void UpdateAnimationState()
    {
        if (_animator == null) return;

        int stateIndex = 3; // По умолчанию Idle

        if (_data.isResting)
        {
            stateIndex = 3;
        }
        else
        {
            switch (_data.status)
            {
                case WorkerStatus.Working: stateIndex = 0; break;
                case WorkerStatus.Sleeping: stateIndex = 1; break;
                case WorkerStatus.Angry: stateIndex = 2; break;
                case WorkerStatus.Fidgeting: stateIndex = 3; break;
                case WorkerStatus.NoEquipment: stateIndex = 3; break;
            }
        }

        // Вызываем один раз. Это предотвратит "дергание" анимации.
        _animator.SetInteger("State", stateIndex);
    }

    // Методы Interact, PerformSlap и PerformKick оставлены без изменений логики
    private async UniTaskVoid PerformKick()
    {
        _isKicking = true;
        UpdateAnimationState(); // Обновим анимацию на Angry

        AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Emotion_Angry").WithVolume(0.2f).RandomSound().At(this.transform));

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
        UpdateAnimationState(); // Вернемся в рабочую анимацию
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

                // --- НОВЫЙ ЗВУК ПРЕМИИ (timescope, индекс 2) ---
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("timescope").ByIndex(2).RandomSound()
                ).Forget();

                int tastyLevel = GameDataManager.Instance.playerPerks.tastyBonusLevel;
                if (tastyLevel > 0)
                {
                    _data.currentAnger = Mathf.Max(0, _data.currentAnger - (tastyLevel * 10f));
                    _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness - (tastyLevel * 10f));
                }
                UpdateAnimationState();
            }
            return;
        }

        PerformSlap();
    }

    private void PerformSlap()
    {
        AudioManager.Instance.PlayAudio(
            AudioQuery.ByKey("Slap").RandomSound()
        ).Forget();

        AudioManager.Instance.StopAudio(AudioQuery.ByKey("Emotion_Sleepy").At(this.transform));

        float baseSlapPower = 40f;
        float perkBonus = baseSlapPower * (0.2f * GameDataManager.Instance.playerPerks.slapLevel);
        float finalSlapEffect = baseSlapPower + perkBonus;

        _data.currentSleepiness = Mathf.Max(0, _data.currentSleepiness - finalSlapEffect);
        _data.currentAnger = Mathf.Min(100, _data.currentAnger + 30f);
        UpdateAnimationState();
    }
}