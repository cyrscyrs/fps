using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class Weapon : MonoBehaviour
{
    public GameObject firePoint;
    public GameObject bulletPre;
    public GameObject firePre;

    public float bulletInterval = .1f;
    [Header("伤害")]
    public float bulletDamage = 25f;      // 一颗子弹造成的伤害（僵尸血量 100，四枪倒地）
    private float timer = 0;
    private PlayerControll pc;
    private RecoliControl rc;

    private ObjectPool<GameObject> bulletPool;
    private ObjectPool<GameObject> firePool;
    public int countActive, countAll, countInactive;

    private void Awake()
    {
        bulletPool = new ObjectPool<GameObject>(
            createBullet,//: () => Instantiate(bulletPre, firePoint.transform),
            actionOnGet,
            actionOnRelease,
            actionOnDestroy,
            true,
            40
        );
        /*
        for (int i = 0; i < 40; i++)
        {
            var p = Instantiate(bulletPre);
            p.gameObject.SetActive( false );
            bulletPool.Release( p );
        }
        */
        firePool = new ObjectPool<GameObject>(
            createFire,
            actionOnGet,
            actionOnRelease,
            actionOnDestroy,
            true,
            40
        );
    }
    void Start()
    {
        pc = GetComponent<PlayerControll>();
        rc = GetComponent<RecoliControl>();
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        countActive = bulletPool.CountActive;
        countAll = bulletPool.CountAll;
        countInactive = bulletPool.CountInactive;

        if(Input.GetMouseButton(0) && timer >= bulletInterval && !pc.isFast)
        {
            timer = 0;
            SpawnBullet();
            SpawnFire();
            rc.Fire();
            //Instantiate(bulletPre, firePoint.transform.position, firePoint.transform.rotation);
        }
    }

    GameObject createBullet()
    {
        GameObject obj = Instantiate(bulletPre);
        obj.GetComponent<Bullet>().pool = bulletPool;
        return obj;
    }

    GameObject createFire()
    {
        GameObject obj = Instantiate(firePre);
        obj.GetComponent<WeaponFire>().pool = firePool;
        return obj;
    }

    private void SpawnBullet()
    {
        GameObject tmp = bulletPool.Get();
        // 伤害值 + 发射者（发射者用来防止子弹打到自己）
        tmp.GetComponent<Bullet>().Shoot(pc.anim.transform.forward, gameObject, bulletDamage);
    }

    private void SpawnFire()
    {
        GameObject tmp = firePool.Get();
        tmp.GetComponent<WeaponFire>().GenerateFire();
    }

    void actionOnGet(GameObject obj) 
    {
        obj.transform.SetPositionAndRotation(firePoint.transform.position, firePoint.transform.rotation);
        obj.gameObject.SetActive(true);

    }

    void actionOnRelease(GameObject obj) 
    {
        obj.gameObject.SetActive(false);
    }

    void actionOnDestroy(GameObject obj) => Destroy(obj);

}
