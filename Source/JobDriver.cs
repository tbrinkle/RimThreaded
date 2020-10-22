﻿#region Assembly Assembly-CSharp, Version=1.2.7558.21380, Culture=neutral, PublicKeyToken=null
// C:\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll
// Decompiled with ICSharpCode.Decompiler 5.0.2.5153
#endregion

using RimWorld;
using System;
using System.Collections.Generic;
using static HarmonyLib.AccessTools;

namespace Verse.AI
{
    public class JobDriver_Patch
    {
        public static FieldRef<JobDriver, int> curToilIndex = FieldRefAccess<JobDriver, int>("curToilIndex");
        public static FieldRef<JobDriver, int> nextToilIndex = FieldRefAccess<JobDriver, int>("nextToilIndex");
        public static FieldRef<JobDriver, bool> wantBeginNextToil = FieldRefAccess<JobDriver, bool>("wantBeginNextToil");
        public static FieldRef<JobDriver, ToilCompleteMode> curToilCompleteMode = FieldRefAccess<JobDriver, ToilCompleteMode>("curToilCompleteMode");
        public static FieldRef<JobDriver, List<Toil>> toils = FieldRefAccess<JobDriver, List<Toil>>("toils");

        private static bool get_CanStartNextToilInBusyStance2(JobDriver __instance)
        {
            int num = curToilIndex(__instance) + 1;
            if (num >= toils(__instance).Count)
            {
                return false;
            }

            return toils(__instance)[num].atomicWithPrevious;
        }
        protected static Toil get_CurToil2(JobDriver __instance)
        {
            if (curToilIndex(__instance) < 0 || __instance.job == null || __instance.pawn.CurJob != __instance.job)
            {
                return null;
            }

            if (curToilIndex(__instance) >= toils(__instance).Count)
            {
                Log.Error(__instance.pawn + " with job " + __instance.pawn.CurJob + " tried to get CurToil with curToilIndex=" + curToilIndex(__instance) + " but only has " + toils(__instance).Count + " toils.");
                return null;
            }

            return toils(__instance)[curToilIndex(__instance)];
        }
        private static bool CheckCurrentToilEndOrFail2(JobDriver __instance)
        {
            try
            {
                Toil curToil = get_CurToil2(__instance);
                if (__instance.globalFailConditions != null)
                {
                    for (int i = 0; i < __instance.globalFailConditions.Count; i++)
                    {
                        JobCondition jobCondition = __instance.globalFailConditions[i]();
                        if (jobCondition != JobCondition.Ongoing)
                        {
                            if (__instance.pawn.jobs.debugLog)
                            {
                                __instance.pawn.jobs.DebugLogEvent(__instance.GetType().Name + " ends current job " + __instance.job.ToStringSafe() + " because of globalFailConditions[" + i + "]");
                            }

                            __instance.EndJobWith(jobCondition);
                            return true;
                        }
                    }
                }

                if (curToil != null && curToil.endConditions != null)
                {
                    for (int j = 0; j < curToil.endConditions.Count; j++)
                    {
                        JobCondition jobCondition2 = curToil.endConditions[j]();
                        if (jobCondition2 != JobCondition.Ongoing)
                        {
                            if (__instance.pawn.jobs.debugLog)
                            {
                                __instance.pawn.jobs.DebugLogEvent(__instance.GetType().Name + " ends current job " + __instance.job.ToStringSafe() + " because of toils[" + curToilIndex + "].endConditions[" + j + "]");
                            }

                            __instance.EndJobWith(jobCondition2);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                JobUtility.TryStartErrorRecoverJob(__instance.pawn, "Exception in CheckCurrentToilEndOrFail for pawn " + __instance.pawn.ToStringSafe(), exception, __instance);
                return true;
            }
        }

        protected static bool get_HaveCurToil2(JobDriver __instance)
        {
            if (curToilIndex(__instance) >= 0 && curToilIndex(__instance) < toils(__instance).Count && __instance.job != null)
            {
                return __instance.pawn.CurJob == __instance.job;
            }

            return false;
        }

        public static bool TryActuallyStartNextToil(JobDriver __instance)
        {
            if (!__instance.pawn.Spawned || (__instance.pawn.stances.FullBodyBusy && !get_CanStartNextToilInBusyStance2(__instance)) || __instance.job == null || __instance.pawn.CurJob != __instance.job)
            {
                return false;
            }
            /*
            if (get_HaveCurToil2(__instance))
            {
                get_CurToil2(__instance).Cleanup(curToilIndex(__instance), __instance);
            }
            */
            if (curToilIndex(__instance) >= 0 && curToilIndex(__instance) < toils(__instance).Count && __instance.job != null)
            {
                if (__instance.pawn.CurJob == __instance.job)
                {
                    Toil curToil2 = toils(__instance)[curToilIndex(__instance)];
                    curToil2.Cleanup(curToilIndex(__instance), __instance);
                }
            }
            
            if (nextToilIndex(__instance) >= 0)
            {
                curToilIndex(__instance) = nextToilIndex(__instance);
                nextToilIndex(__instance) = -1;
            }
            else
            {
                curToilIndex(__instance)++;
            }

            wantBeginNextToil(__instance) = false;

            if (!get_HaveCurToil2(__instance))
            {
                if (__instance.pawn.stances != null && __instance.pawn.stances.curStance.StanceBusy)
                {
                    Log.ErrorOnce(__instance.pawn.ToStringSafe() + " ended job " + __instance.job.ToStringSafe() + " due to running out of toils during a busy stance.", 6453432);
                }

                __instance.EndJobWith(JobCondition.Succeeded);
                return false;
            }


            __instance.debugTicksSpentThisToil = 0;
            __instance.ticksLeftThisToil = get_CurToil2(__instance).defaultDuration;
            curToilCompleteMode(__instance) = get_CurToil2(__instance).defaultCompleteMode;
            if (CheckCurrentToilEndOrFail2(__instance))
            {
                return false;
            }

            Toil curToil = get_CurToil2(__instance);
            if (get_CurToil2(__instance).preInitActions != null)
            {
                for (int i = 0; i < get_CurToil2(__instance).preInitActions.Count; i++)
                {
                    try
                    {
                        get_CurToil2(__instance).preInitActions[i]();
                    }
                    catch (Exception exception)
                    {
                        JobUtility.TryStartErrorRecoverJob(__instance.pawn, "JobDriver threw exception in preInitActions[" + i + "] for pawn " + __instance.pawn.ToStringSafe(), exception, __instance);
                        return false;
                    }

                    if (get_CurToil2(__instance) != curToil)
                    {
                        break;
                    }
                }
            }

            if (get_CurToil2(__instance) == curToil)
            {
                if (get_CurToil2(__instance).initAction != null)
                {
                    try
                    {
                        get_CurToil2(__instance).initAction();
                    }
                    catch (Exception exception2)
                    {
                        JobUtility.TryStartErrorRecoverJob(__instance.pawn, "JobDriver threw exception in initAction for pawn " + __instance.pawn.ToStringSafe(), exception2, __instance);
                        return false;
                    }
                }

                if (!__instance.ended && curToilCompleteMode(__instance) == ToilCompleteMode.Instant && get_CurToil2(__instance) == curToil)
                {
                    __instance.ReadyForNextToil();
                }
            }
            return false;
        }


    }
}