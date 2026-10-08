using UnityEngine;

[RequireComponent(typeof(ChestState))]
public class ChestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] Transform lid;                                  // yung "top" ng chest
    [SerializeField] Vector3 openEuler = new Vector3(-75f, 0f, 0f);  // ibalik ang sign kung baliktad
    [SerializeField] float openSpeed = 4f;

    ChestState state;
    Quaternion closedRot;
    Quaternion openRot;

    void Awake()
    {
        state = GetComponent<ChestState>();
        closedRot = lid.localRotation;
        openRot = closedRot * Quaternion.Euler(openEuler);
    }

    public string Prompt => state.IsOpen ? "Press E to close" : "Press E to open";
    public bool CanInteract => true;
    public int Priority => 0;

    public void Interact(PlayerInventory inv)
    {
        state.IsOpen = !state.IsOpen;
    }

    void Update()
    {
        lid.localRotation = Quaternion.Slerp(
            lid.localRotation,
            state.IsOpen ? openRot : closedRot,
            openSpeed * Time.deltaTime
        );
    }
}