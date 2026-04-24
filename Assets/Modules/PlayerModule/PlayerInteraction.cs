using Assets.Modules.Interractables;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Modules.PlayerModule
{
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private Camera playerCamera;

        public void PerformInteraction(InputAction.CallbackContext obj)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
            {
                // Пытаемся получить компонент через интерфейс
                if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    interactable.Interact(this.gameObject);
                }
            }
        }
    }
}
