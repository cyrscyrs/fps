using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControll : MonoBehaviour
{
    private float speed = 3f;
    private float jumpForce = 5f;
    private Vector3 velocity;

    private float xScensitivity = 10;
    private float yScensitivity = 10;

    public Rigidbody rb;
    public Animator anim;

    private float xRotation = 0;
    private bool jump;

    public bool isAiming = false;
    public bool isFast = false;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Aim();
        Mouse();
        MoveFast();
        Move();
        Jump();
    }

    private void FixedUpdate()
    {
        if (jump)
        {
            jump = false;
            velocity.y = jumpForce;
        }
        rb.velocity = velocity;
    }

    private void Aim()
    {
        if(Input.GetMouseButton(1))
        {
            isAiming = true;
            anim.SetBool("Aim", true);
            float aim = anim.GetFloat("Aiming");
            anim.SetFloat("Aiming", Mathf.Lerp(aim, 1, .1f));
        }
        else
        {
            isAiming = false;
            anim.SetBool("Aim", false);
            float aim = anim.GetFloat("Aiming");
            anim.SetFloat("Aiming", Mathf.Lerp(aim, 0, .1f));
        }
    }

    private void Mouse()
    {
        float x = Input.GetAxis("Mouse X");
        float y = Input.GetAxis("Mouse Y");

        xRotation -= y * yScensitivity;
        xRotation = Mathf.Clamp(xRotation, -80, 80);
        anim.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        transform.Rotate(Vector3.up * x * xScensitivity);
    }
    private void Move()
    {
        float horizontal = Input.GetAxis("Horizontal"), vertical = Input.GetAxis("Vertical");
        Vector3 dir = (transform.forward * vertical + transform.right * horizontal).normalized;
        velocity = dir * speed;
        velocity.y = rb.velocity.y;

        anim.SetFloat("Movement", dir.magnitude);
    }

    void MoveFast()
    {
        if(Input.GetKey(KeyCode.LeftShift) && IsGrounded())
        {
            isFast = true;
            speed = 5;
            anim.SetBool("Holstered", true);
        }
        else
        {
            isFast = false;
            speed = 3;
            anim.SetBool("Holstered", false);
        }
    }

    private void Jump()
    {
        if(Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            jump = true;
        }
    }

    public bool IsGrounded()
    {
        RaycastHit hit;
        bool res = Physics.Raycast(transform.position + Vector3.up * .2f, -Vector3.up, out hit,
            .4f, LayerMask.GetMask("Ground"));
        return res;
    }
}
