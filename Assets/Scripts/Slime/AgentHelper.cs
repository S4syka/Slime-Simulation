using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Slime
{
    public static class AgentHelper
    {
        public static Agent[] GenerateInitialPositions(int number, SpawnMode spawnMode, int width, int height, int speciesCount)
        {
            Agent[] agents = new Agent[number];

            for (int i = 0; i < agents.Length; i++)
            {
                Vector2 centre = new Vector2(width / 2, height / 2);
                Vector2 startPos = Vector2.zero;
                float randomAngle = UnityEngine.Random.value * Mathf.PI * 2;
                float angle = 0;

                // Determine angle and position.
                if (spawnMode == SpawnMode.Point)
                {
                    startPos = centre;
                    angle = randomAngle;
                }
                else if (spawnMode == SpawnMode.Random)
                {
                    startPos = new Vector2(UnityEngine.Random.Range(0, width), UnityEngine.Random.Range(0, height));
                    angle = randomAngle;
                }
                else if (spawnMode == SpawnMode.InwardCircle)
                {
                    startPos = centre + UnityEngine.Random.insideUnitCircle * height * 0.5f;
                    angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
                }
                else if (spawnMode == SpawnMode.RandomCircle)
                {
                    startPos = centre + UnityEngine.Random.insideUnitCircle * height * 0.15f;
                    angle = randomAngle;
                }

                Vector3Int speciesMask;
                int speciesIndex = 0;

                if (speciesCount == 1)
                {
                    speciesMask = Vector3Int.one;
                }
                else
                {
                    int species = UnityEngine.Random.Range(1, speciesCount + 1);
                    speciesIndex = species - 1;
                    speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
                }

                agents[i] = new Agent() { position = startPos, angle = angle, speciesMask = speciesMask, speciesIndex = speciesIndex };
            }

            return agents;
        }
    }

    public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

    public struct Agent
    {
        public Vector2 position;
        public float angle;
        public Vector3Int speciesMask;
        int unusedSpeciesChannel;
        public int speciesIndex;
        public float4 colour;
    }
}