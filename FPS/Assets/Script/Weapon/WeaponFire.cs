using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class WeaponFire : MonoBehaviour
{
    public ObjectPool<GameObject> pool;
    public float lifeTime = .05f;
    private Coroutine lifeCoroutine;
    void Start()
    {
        
    }

    void Update()
    {

    }

    public void GenerateFire()
    {
        if (lifeCoroutine != null) StopCoroutine(lifeCoroutine);
        lifeCoroutine = StartCoroutine(ReleaseForDelay());
    }

    IEnumerator ReleaseForDelay()
    {
        yield return new WaitForSeconds(lifeTime);
        pool.Release(gameObject);
    }
}
