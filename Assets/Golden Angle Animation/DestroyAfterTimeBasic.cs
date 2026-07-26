using UnityEngine;

public class DestroyAfterTimeBasic : MonoBehaviour
{
    public float time = 2f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Destroy(gameObject, time);
    }
}
