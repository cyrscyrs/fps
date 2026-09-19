using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class BulletPool : MonoBehaviour
{
    public GameObject bulletPre;
    private ObjectPool<GameObject> pool;
    private void Awake()
    {
        pool = new ObjectPool<GameObject>(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, true, 50);
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    GameObject createFunc()
    {
        var obj = Instantiate(bulletPre, transform);
        return obj;
    }

    void actionOnGet(GameObject obj) => obj.gameObject.SetActive(true);
    void actionOnRelease(GameObject obj) => obj.gameObject.SetActive(false);
    void actionOnDestroy(GameObject obj) => Destroy(obj.gameObject);
}
