﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimThreaded
{

    public class HediffSet_Patch
    {
        public static AccessTools.FieldRef<HediffSet, List<Hediff_MissingPart>> cachedMissingPartsCommonAncestors =
            AccessTools.FieldRefAccess<HediffSet, List<Hediff_MissingPart>>("cachedMissingPartsCommonAncestors");
        public static AccessTools.FieldRef<HediffSet, Queue<BodyPartRecord>> missingPartsCommonAncestorsQueue =
            AccessTools.FieldRefAccess<HediffSet, Queue<BodyPartRecord>>("missingPartsCommonAncestorsQueue");
        public static bool CacheMissingPartsCommonAncestors(HediffSet __instance)
        {

            if (cachedMissingPartsCommonAncestors(__instance) == null)
            {
                cachedMissingPartsCommonAncestors(__instance) = new List<Hediff_MissingPart>();
            }

            lock (cachedMissingPartsCommonAncestors(__instance))
            {
                cachedMissingPartsCommonAncestors(__instance).Clear();
                missingPartsCommonAncestorsQueue(__instance).Clear();
                missingPartsCommonAncestorsQueue(__instance).Enqueue(__instance.pawn.def.race.body.corePart);
                while (missingPartsCommonAncestorsQueue(__instance).Count != 0)
                {
                    BodyPartRecord node = missingPartsCommonAncestorsQueue(__instance).Dequeue();
                    if (node != null)
                    {
                        if (__instance.PartOrAnyAncestorHasDirectlyAddedParts(node))
                        {
                            continue;
                        }

                        Hediff_MissingPart hediff_MissingPart = (from x in __instance.GetHediffs<Hediff_MissingPart>()
                                                                 where x.Part == node
                                                                 select x).FirstOrDefault();
                        if (hediff_MissingPart != null)
                        {
                            cachedMissingPartsCommonAncestors(__instance).Add(hediff_MissingPart);
                            continue;
                        }

                        for (int i = 0; i < node.parts.Count; i++)
                        {
                            missingPartsCommonAncestorsQueue(__instance).Enqueue(node.parts[i]);
                        }
                    }
                }
            }
            return false;
        }

    }
}
