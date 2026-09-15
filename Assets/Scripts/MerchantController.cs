using UnityEngine;
using UnityEngine.InputSystem;

// A single persistent shop merchant. GameCycleManager calls Reposition() at the start of every
// dig phase (the very first included) to move it to wherever TerrainGenerator placed the shop
// room this cycle, or hide it if no shop was generated. Clicking it in range fires OnMerchantClicked;
// GameCycleManager owns everything about what happens after that (opening the shop UI, prices, etc.),
// same as it owns the tool-upgrade purchases in the Upgrading phase.
public class MerchantController : MonoBehaviour
{
    public float clickRadius = 1.2f;

    public event System.Action OnMerchantClicked;

    // Called by GameCycleManager. Null means no shop exists this cycle.
    public void Reposition(Vector3Int? interiorCell)
    {
        if (interiorCell.HasValue)
        {
            transform.position = new Vector3(interiorCell.Value.x + 0.5f, interiorCell.Value.y + 0.5f, 0f);
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (GameCycleManager.CurrentPhase != GamePhase.Digging) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        if (Vector2.Distance(transform.position, worldPos) <= clickRadius)
            OnMerchantClicked?.Invoke();
    }
}
