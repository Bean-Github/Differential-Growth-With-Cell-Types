using UnityEngine;

public class Textbox : MonoBehaviour
{
    public KeyCode activationKey;



    public GameObject textboxUI;

    public Animator animator;

    private void Start()
    {
        textboxUI.SetActive(false);
    }


    private void Update()
    {
        if (Input.GetKeyDown(activationKey))
        {
            ToggleTextbox();

            animator.Play("Appear");
        }
    }


    void ToggleTextbox()
    {
        textboxUI.SetActive(!textboxUI.activeSelf);
    }


}
