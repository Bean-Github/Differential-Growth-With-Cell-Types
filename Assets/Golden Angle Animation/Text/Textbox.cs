using System.Collections;
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

        }
    }


    void ToggleTextbox()
    {
        if (!textboxUI.activeSelf)
        {
            textboxUI.SetActive(true);
            animator.Play("Appear");
        }
        else
        {
            StartCoroutine(DisableWithDelay());
        }

    }

    IEnumerator DisableWithDelay()
    {
        animator.Play("Disappear");

        yield return new WaitForSeconds(0.5f); // Adjust the delay as needed
        textboxUI.SetActive(false);
    }


}
