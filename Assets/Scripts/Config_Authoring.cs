using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameOfLife
{
    public class ConfigAuthoring : MonoBehaviour
    {
        public int GridSize;

        public bool useRandomInitialization;
        public List<int2> InitialAliveCells;

        public RenderingMode RenderingMode;
        public float UpdatesPerSecond;
    }

    public class ConfigBaker : Baker<ConfigAuthoring>
    {
        public override void Bake(ConfigAuthoring authoring)
        {
            var e = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(e, new Config
            {
                GridSize = authoring.GridSize,
                UpdatesPerSecond = authoring.UpdatesPerSecond,
                RenderingMode = authoring.RenderingMode,
                UseRandomInitialization = authoring.useRandomInitialization
            });



            var buffer = AddBuffer<InitialAliveCells>(e);
            for (int i = 0; i < authoring.InitialAliveCells.Count; i++)
            {
                buffer.Add(new InitialAliveCells { Value = authoring.InitialAliveCells[i] });
            }

        }
    }
}
