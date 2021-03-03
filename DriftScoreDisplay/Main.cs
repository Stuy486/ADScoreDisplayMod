using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;


namespace DriftScoreDisplay
{
    public struct TickScore
    {
        public const int NUM_TICK_SCORES = 500;
        public TickScore(float score, float time)
        {
            Score = score;
            Time = time;
        }

        public float Score { get; }
        public float Time  { get; }

        public override string ToString() => $"({Time}, {Score})";
    }

    static class Main
    {
        public static UnityModManager.ModEntry mod; // despite usual coding best practises, leaving your mod fields as public is best, enabling other modders to interact with your mod.
        public static bool enabled = true;
        public static PlayerManager currentPlayerManager;
        public static ScoringDisplay scoreDisplay;
        public static Queue<TickScore> tickScores;
        public static float secondTotal;
        public static float elapsedTime;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                var harmony = new Harmony(modEntry.Info.Id);

                mod = modEntry;
                modEntry.OnToggle = OnToggleEnabled;
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                mod.Logger.Error(e.ToString());
            }

            return true;
        }

        static bool OnToggleEnabled(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            if (!enabled && scoreDisplay != null)
            {
                scoreDisplay.Destroy();
                scoreDisplay = null;
                currentPlayerManager = null;
            }
            return true;
        }

        [HarmonyPatch(typeof(PlayerManager))]
        [HarmonyPatch("Update")]
        static class ResetPatch
        {
            static void Postfix(ref PlayerManager __instance)
            {
                try
                {
                    if (enabled && !__instance.Equals(currentPlayerManager))
                    {
                        currentPlayerManager = __instance;
                        scoreDisplay = new ScoringDisplay();
                        scoreDisplay.Create(currentPlayerManager);

                        tickScores = new Queue<TickScore>(TickScore.NUM_TICK_SCORES);
                        secondTotal = 0f;
                        elapsedTime = 0f;
                    }
                }
                catch (Exception e)
                {
                    mod.Logger.Error(e.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(PointsManager))]
        [HarmonyPatch("Update")]
        static class UpdatePatch
        {
            public static float maxPoints = 0f;
            static void Prefix(ref PointsManager __instance)
            {
                try
                {
                    if (!enabled || scoreDisplay == null) return;

                    elapsedTime += Time.deltaTime;
                    Vector3 vector = GameEntryPoint.levelManager.playerManager.playerRigidBody.transform.InverseTransformDirection(
                        GameEntryPoint.levelManager.playerManager.playerRigidBody.velocity);

                    float currentPoints = 0f;
                    if (vector.z > 2f && __instance.playerCarDynamics.AllWheelsOnGround() && Mathf.Abs(vector.x) > 11f)
                    {
                        currentPoints = 50f * Mathf.Abs(vector.x * Time.deltaTime);
                    }

                    secondTotal += currentPoints;
                    tickScores.Enqueue(new TickScore(currentPoints, elapsedTime));
                    while (tickScores.Count > 0 && tickScores.Peek().Time < elapsedTime - 1f)
                    {
                        secondTotal -= tickScores.Dequeue().Score;
                    }

                    maxPoints = Mathf.Max(maxPoints, secondTotal);
                    if (maxPoints > 0f) {
                        float curPercent = Mathf.Clamp(secondTotal / maxPoints, 0f, 1f);
                        scoreDisplay.image.color = new Color(1 - curPercent, curPercent, 0f);
                        if (secondTotal != 0f)
                        {
                            scoreDisplay.text.text = string.Format("{0:D}", 10 * (int)(secondTotal / 10f));
                        } else
                        {
                            scoreDisplay.text.text = string.Format("--");
                        }
                    }
                }
                catch (Exception e)
                {
                    mod.Logger.Error(e.ToString());
                }
            }
        }

        public class ScoringDisplay
        {
            public GameObject scoreDisplayObject;
            public RawImage image;
            public Text text;

            public void Create(PlayerManager playerManager)
            {
                try
                {
                    GameObject gearIndicator = playerManager.gearDisplayGameObject;
                    scoreDisplayObject = UnityEngine.Object.Instantiate(gearIndicator, gearIndicator.transform.parent);

                    image = scoreDisplayObject.transform.GetComponentInChildren<RawImage>();
                    image.color = Color.red;
                    text = scoreDisplayObject.transform.Find("label").GetComponent<Text>();
                    text.text = string.Format("--");

                    scoreDisplayObject.transform.Translate((Screen.width / 2) - 50, Screen.height / 10, 0);
                    scoreDisplayObject.SetActive(true);
                }
                catch (Exception e)
                {
                    mod.Logger.Error(e.ToString());
                }
            }
            public void Destroy()
            {
                try
                {
                    if (scoreDisplayObject != null)
                    {
                        UnityEngine.Object.Destroy(scoreDisplayObject);
                    }
                }
                catch (Exception e)
                {
                    mod.Logger.Error(e.ToString());
                }
            }
        }
    }
}