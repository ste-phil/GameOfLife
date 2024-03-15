using System;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameOfLife
{
    public class UI : MonoBehaviour
    {
        UIDocument UIDocument;

        private int axisSize;
        private float simulationTickRate;

        private bool resettingState;

        private void Start()
        {
            UIDocument = GetComponent<UIDocument>();
            UIDocument.rootVisualElement.Q<Toggle>("pauseToggle").RegisterValueChangedCallback<bool>(OnPauseToggled);
            UIDocument.rootVisualElement.Q<Button>("resetBtn").RegisterCallback<ClickEvent>(OnResetBtnClicked);
            UIDocument.rootVisualElement.Q<TextField>("axisSizeInput").isDelayed = true;
            UIDocument.rootVisualElement.Q<TextField>("axisSizeInput").RegisterValueChangedCallback<string>(OnAxisSizeChanged);
            UIDocument.rootVisualElement.Q<TextField>("updateRate").RegisterValueChangedCallback<string>(OnSimulationUpdateRateChanged);


            axisSize = Convert.ToInt32(UIDocument.rootVisualElement.Q<TextField>("axisSizeInput").value);
            simulationTickRate = Convert.ToSingle(UIDocument.rootVisualElement.Q<TextField>("updateRate").value);

            ResetAndUpdateSimulation();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var isPaused = Time.timeScale == 0.0f;

                var pauseToggle = UIDocument.rootVisualElement.Q<Toggle>("pauseToggle");
                pauseToggle.SetValueWithoutNotify(!isPaused);

                Time.timeScale = !isPaused ? 1.0f : 0.0f;
            }
        }

        void OnResetBtnClicked(ClickEvent e)
        {
            ResetAndUpdateSimulation();
        }

        void OnPauseToggled(ChangeEvent<bool> evt)
        {
            Time.timeScale = !evt.newValue ? 1.0f : 0.0f;
        }

        void OnAxisSizeChanged(ChangeEvent<string> evt)
        {
            //Axis is required to be a multiple of 32 because of the BRGMultiCellGraph rendering mode 
            if (int.TryParse(evt.newValue, out var newAxisSize) && newAxisSize % 32 == 0)
            {
                axisSize = newAxisSize;
                ResetAndUpdateSimulation();
            }
            else
            {
                UIDocument.rootVisualElement.Q<TextField>("axisSizeInput").value = axisSize.ToString();
            }
        }

        private void OnSimulationUpdateRateChanged(ChangeEvent<string> evt)
        {
            if (float.TryParse(evt.newValue, out var newSimulationTickRate))
            {
                simulationTickRate = newSimulationTickRate;

                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                var query = em.CreateEntityQuery(typeof(Config));

                if (query.CalculateEntityCountWithoutFiltering() == 0)
                    return;
                
                var e = query.ToEntityArray(Allocator.Temp)[0];
                em.SetComponentData(e, new Config
                {
                    GridSize = axisSize,
                    RenderingMode = RenderingMode.BRGMultiCellGraphInstanced,
                    UpdatesPerSecond = simulationTickRate,
                    UseRandomInitialization = true,
                });
            }
            else
            {
                UIDocument.rootVisualElement.Q<TextField>("updateRate").value = simulationTickRate.ToString();
            }
        }

        private void ResetAndUpdateSimulation()
        {
            if (resettingState)
                return;

            Debug.Log("Reset!");
            StartCoroutine(ResetAndUpdateSimulationCoroutine());
        }

        private IEnumerator ResetAndUpdateSimulationCoroutine()
        {
            resettingState = true;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var query = em.CreateEntityQuery(typeof(ActiveSimulation));
            em.DestroyEntity(query);

            query = em.CreateEntityQuery(typeof(Config));
            em.DestroyEntity(query);

            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            var configEntity = em.CreateEntity();
            em.AddComponentData(configEntity, new Config
            {
                GridSize = axisSize,
                RenderingMode = RenderingMode.BRGMultiCellGraphInstanced,
                UpdatesPerSecond = simulationTickRate,
                UseRandomInitialization = true,
            });
            em.AddBuffer<InitialAliveCells>(configEntity);

            //                        Cell count        / cellsPerInstance
            var instanceCount = (axisSize) * (axisSize) / 32;
            //                    instanceCount *  Instance size   + visible instance array size
            var renderingMemory = instanceCount * (2 * 3 * 16 + 4) + 4 * instanceCount;

            UIDocument.rootVisualElement.Q<Label>("totalCellsLabel").text = $"{FormatTotalCells(axisSize * axisSize)}";
            UIDocument.rootVisualElement.Q<Label>("memUsageRendering").text = $"{BitsToHumanReadable(renderingMemory)}";
            UIDocument.rootVisualElement.Q<Label>("memUsageSimulation").text = $"{BitsToHumanReadable((long)(math.pow(axisSize+2, 2) * 2))}";

            resettingState = false;
        }


        public static string BitsToHumanReadable(long bits)
        {
            if (bits < 0)
                return "N/A";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double size = bits / 8;
            int index = 0;

            while (size >= 1024 && index < sizes.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size:0.##} {sizes[index]}";
        }


        public static string FormatTotalCells(long number)
        {
            string numberString = number.ToString();
            int length = numberString.Length;
            int remainder = length % 3;
            string formattedNumber = "";

            // Handle the digits before the first group of three
            if (remainder != 0)
            {
                formattedNumber += numberString.Substring(0, remainder) + ",";
            }

            // Handle the rest of the digits in groups of three
            for (int i = remainder; i < length; i += 3)
            {
                formattedNumber += numberString.Substring(i, 3);
                if (i + 3 < length)
                {
                    formattedNumber += ",";
                }
            }

            return formattedNumber;
        }
    }
}
