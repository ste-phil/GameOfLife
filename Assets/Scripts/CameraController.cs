using GameOfLife;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float ScrollSpeed;


    void Update()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<Config>(), ComponentType.ReadOnly<ActiveSimulation>());
        if (query.CalculateEntityCount() == 0)
            return;
        
        var config = query.ToComponentDataArray<Config>(Allocator.Temp)[0];
        var axis = Input.GetAxis("Mouse ScrollWheel");

        var dt = Time.deltaTime;    
        Camera.main.orthographicSize = math.clamp(
            Camera.main.orthographicSize - axis * ScrollSpeed * dt,
            config.OrthographicSize * .4f,
            config.OrthographicSize * 1.2f
        );

    }
}
