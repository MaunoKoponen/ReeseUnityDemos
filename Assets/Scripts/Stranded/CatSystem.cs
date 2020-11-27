﻿using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reese.Demo
{
    class CatSystem : SystemBase
    {
        EntityCommandBufferSystem barrier => World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        GameObject catGO = default;

        protected override void OnCreate()
        {
            if (!SceneManager.GetActiveScene().name.Equals("Stranded"))
            {
                Enabled = false;
                return;
            }

            catGO = GameObject.Find("Cat");
        }

        protected override void OnUpdate()
        {
            if (catGO == null) return;

            var commandBuffer = barrier.CreateCommandBuffer();

            var elapsedSeconds = (float)Time.ElapsedTime;

            Entities
                .WithStructuralChanges()
                .WithChangeFilter<SpatialEvent>()
                .WithNone<Hopping>()
                .ForEach((Entity entity, in Cat cat, in Translation translation) =>
                {
                    var meowController = catGO.GetComponent<CatMeowController>();

                    if (meowController == null) return;

                    meowController.Meow();

                    commandBuffer.AddComponent(entity, new Hopping
                    {
                        OriginalPosition = translation.Value,
                        Height = 10,
                        StartSeconds = elapsedSeconds,
                        Duration = 1
                    });
                })
                .WithName("CatMeowJob")
                .Run();

            var parallelCommandBuffer = commandBuffer.AsParallelWriter();

            Entities
                .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation, in Hopping hopping) =>
                {
                    var position = translation.Value;

                    position.y = hopping.Height * math.sin(
                        math.PI * ((elapsedSeconds - hopping.StartSeconds) / hopping.Duration)
                    );

                    translation.Value = position;

                    if (elapsedSeconds - hopping.StartSeconds < hopping.Duration) return;

                    translation.Value = hopping.OriginalPosition;
                    parallelCommandBuffer.RemoveComponent<Hopping>(entityInQueryIndex, entity);
                })
                .WithName("CatHopJob")
                .ScheduleParallel();

            barrier.AddJobHandleForProducer(Dependency);
        }
    }
}
