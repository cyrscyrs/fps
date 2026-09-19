using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecoliControl : MonoBehaviour
{
    public float power = -3f;
    public float speed = 10;
    public float returnSpeed = 5;

    private float targetRotation;
    private float currentRotation;

    private Animator anim;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        
    }

    // Update is called once per frame
    void Update()
    {
        targetRotation = Mathf.Lerp(targetRotation, 0, returnSpeed * Time.deltaTime);
        currentRotation = Mathf.Lerp(currentRotation, targetRotation, speed * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(currentRotation, transform.localEulerAngles.y, 0);
        //anim.transform.localRotation = Quaternion.Euler(currentRotation, transform.localEulerAngles.y, 0);
    }

    public void Fire()
    {
        targetRotation += power;
    }
}
