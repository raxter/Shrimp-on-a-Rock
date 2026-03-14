using UnityEngine;
using UnityEngine.InputSystem;

public class DudeLauncher : MonoBehaviour
{
    public Dude dudePrefab;
    public InputActionReference moveAction;

    void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.Enable();
            moveAction.action.performed += OnMovePerformed;
        }
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.action.performed -= OnMovePerformed;
        }
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        Launch();
    }

    void Launch()
    {
        if (dudePrefab == null || TheRock.Instance == null) return;

        Dude dude = Instantiate(dudePrefab, transform.position, Quaternion.identity);
        LaunchedDude launched = dude.gameObject.AddComponent<LaunchedDude>();
        launched.source = transform;
        launched.target = TheRock.Instance.Hilltop;
    }
}
