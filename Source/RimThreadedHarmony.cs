﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using Verse.AI;
using RimWorld;
using Verse.Sound;
using RimWorld.Planet;
using System.Security.Policy;
using System.Reflection.Emit;
using System.Threading;
using Verse.Grammar;

namespace RimThreaded
{

	[StaticConstructorOnStartup]
	public class RimThreadedHarmony {

		public static BindingFlags bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static Harmony harmony = new Harmony("majorhoff.rimthreaded");
		public static Type giddyUpCoreUtilitiesTextureUtility;
		public static Type giddyUpCoreStorageExtendedDataStorage;
		public static Type giddyUpCoreStorageExtendedPawnData;
		public static Type giddyUpCoreJobsJobDriver_Mounted;
		public static Type giddyUpCoreJobsGUC_JobDefOf;
		public static Type hospitalityCompUtility;
		public static Type hospitalityCompGuest;
		public static Type giddyUpCoreHarmonyPawnJobTracker_DetermineNextJob;
		public static Type awesomeInventoryJobsJobGiver_FindItemByRadius;
		public static Type awesomeInventoryErrorMessage;
		public static Type jobGiver_AwesomeInventory_TakeArm;
		public static Type awesomeInventoryJobsJobGiver_FindItemByRadiusSub;
		public static Type pawnRulesPatchRimWorld_Pawn_GuestTracker_SetGuestStatus;
		public static Type combatExtendedCE_Utility;
		public static Type combatExtendedVerb_LaunchProjectileCE;
		public static Type combatExtendedVerb_MeleeAttackCE;

		public static List<CodeInstruction> GetLockCodeInstructions(
			ILGenerator iLGenerator, List<CodeInstruction> instructionsList, int currentInstructionIndex,
			int searchInstructionsCount, List<CodeInstruction> loadLockObjectInstructions,
			LocalBuilder lockObject, LocalBuilder lockTaken)
		{
			List<CodeInstruction> finalCodeInstructions = new List<CodeInstruction>();
			loadLockObjectInstructions[0].labels = instructionsList[currentInstructionIndex].labels;
			for (int i = 0; i < loadLockObjectInstructions.Count; i++)
			{
				finalCodeInstructions.Add(loadLockObjectInstructions[i]);
			}
			instructionsList[currentInstructionIndex].labels = new List<Label>();
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Stloc, lockObject.LocalIndex));
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Ldc_I4_0));
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Stloc, lockTaken.LocalIndex));
			CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldloc, lockObject.LocalIndex);
			codeInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
			finalCodeInstructions.Add(codeInstruction);
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Ldloca_S, lockTaken.LocalIndex));
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Monitor), "Enter", 
				new Type[] { typeof(object), typeof(bool).MakeByRefType() })));
			for (int i = 0; i < searchInstructionsCount; i++)
			{
				finalCodeInstructions.Add(instructionsList[currentInstructionIndex]);
				currentInstructionIndex++;
			}
			Label endHandlerDestination = iLGenerator.DefineLabel();
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Leave_S, endHandlerDestination));
			codeInstruction = new CodeInstruction(OpCodes.Ldloc, lockTaken.LocalIndex);
			codeInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
			finalCodeInstructions.Add(codeInstruction);
			Label endFinallyDestination = iLGenerator.DefineLabel();
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Brfalse_S, endFinallyDestination));
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Ldloc, lockObject.LocalIndex));
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Monitor), "Exit")));
			codeInstruction = new CodeInstruction(OpCodes.Endfinally);
			codeInstruction.labels.Add(endFinallyDestination);
			codeInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
			finalCodeInstructions.Add(codeInstruction);
			instructionsList[currentInstructionIndex].labels.Add(endHandlerDestination);
			return finalCodeInstructions;
		}
		public static List<CodeInstruction> GetLockCodeInstructions(
			ILGenerator iLGenerator, List<CodeInstruction> instructionsList, int currentInstructionIndex,
			int searchInstructionsCount, List<CodeInstruction> loadLockObjectInstructions, Type lockObjectType)
		{
			LocalBuilder lockObject = iLGenerator.DeclareLocal(lockObjectType);
			LocalBuilder lockTaken = iLGenerator.DeclareLocal(typeof(bool));
			return GetLockCodeInstructions(iLGenerator, instructionsList, currentInstructionIndex,
				searchInstructionsCount, loadLockObjectInstructions, lockObject, lockTaken);
		}

		public static bool IsCodeInstructionsMatching(List<CodeInstruction> searchInstructions, List<CodeInstruction> instructionsList, int instructionIndex)
		{
			bool instructionsMatch = false;
			if (instructionIndex + searchInstructions.Count < instructionsList.Count)
			{
				instructionsMatch = true;
				for (int searchIndex = 0; searchIndex < searchInstructions.Count; searchIndex++)
				{
					CodeInstruction searchInstruction = searchInstructions[searchIndex];
					CodeInstruction originalInstruction = instructionsList[instructionIndex + searchIndex];
					object searchOperand = searchInstruction.operand;
					object orginalOperand = originalInstruction.operand;
					if (searchInstruction.opcode != originalInstruction.opcode)
					{
						instructionsMatch = false;
						break;
					}
					else
					{
						if (orginalOperand != null &&
							searchOperand != null &&
							orginalOperand != searchOperand)
						{
							if (orginalOperand.GetType() != typeof(LocalBuilder))
							{
								instructionsMatch = false;
								break;
							}
							else
							{
								if (((LocalBuilder)orginalOperand).LocalIndex != (int)searchOperand)
								{
									instructionsMatch = false;
									break;
								}
							}
						}
					}
				}
			}
			return instructionsMatch;
		}
		public static Label GetBreakDestination(List<CodeInstruction> instructionsList, int currentInstructionIndex, ILGenerator iLGenerator)
		{
			Label breakDestination = iLGenerator.DefineLabel();
			HashSet<Label> labels = new HashSet<Label>();
			for (int i = 0; i <= currentInstructionIndex; i++)
			{
				foreach (Label label in instructionsList[i].labels)
				{
					labels.Add(label);
				}
			}
			for (int i = currentInstructionIndex + 1; i < instructionsList.Count; i++)
			{
				if (instructionsList[i].operand is Label label)
				{
					if (labels.Contains(label))
					{
						instructionsList[i + 1].labels.Add(breakDestination);
						break;
					}
				}
			}
			return breakDestination;
		}
		public static List<CodeInstruction> UpdateTryCatchCodeInstructions(ILGenerator iLGenerator, 
			List<CodeInstruction> instructionsList, int currentInstructionIndex, int searchInstructionsCount)
		{
			Label breakDestination = GetBreakDestination(instructionsList, currentInstructionIndex, iLGenerator);
			List<CodeInstruction> finalCodeInstructions = new List<CodeInstruction>();
			instructionsList[currentInstructionIndex].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
			for (int i = 0; i < searchInstructionsCount; i++)
			{
				finalCodeInstructions.Add(instructionsList[currentInstructionIndex]);
				currentInstructionIndex++;
			}
			Label handlerEnd = iLGenerator.DefineLabel();
			finalCodeInstructions.Add(new CodeInstruction(OpCodes.Leave_S, handlerEnd));
			CodeInstruction pop = new CodeInstruction(OpCodes.Pop);
			pop.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(ArgumentOutOfRangeException)));
			finalCodeInstructions.Add(pop);
			CodeInstruction leaveLoopEnd = new CodeInstruction(OpCodes.Leave, breakDestination);
			leaveLoopEnd.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
			finalCodeInstructions.Add(leaveLoopEnd);
			instructionsList[currentInstructionIndex].labels.Add(handlerEnd);
			//finalCodeInstructions.Add(instructionsList[currentInstructionIndex]);
			return finalCodeInstructions;
		}

		static RimThreadedHarmony() {
			Harmony.DEBUG = true;
			Log.Message("RimThreaded Harmony is loading...");
			Type original = null;
			Type patched = null;
			
			//ContentFinderTexture2D			
			original = typeof(ContentFinder<Texture2D>);
			patched = typeof(ContentFinder_Texture2D_Patch);
			Prefix(original, patched, "Get");

			//ContentFinderTexture2D			
			original = typeof(MaterialPool);
			patched = typeof(MaterialPool_Patch);
			Prefix(original, patched, "MatFrom", new Type[] { typeof(MaterialRequest) });

			//TickList			
			original = typeof(TickList);
			patched = typeof(TickList_Patch);
			Prefix(original, patched, "Tick");
			Prefix(original, patched, "RegisterThing");
			Prefix(original, patched, "DeregisterThing");

			//Rand
			original = typeof(Rand);
			//patched = typeof(Rand_Patch);
			//Prefix(original, patched, "set_Seed");
			//Prefix(original, patched, "get_Value");
			//Prefix(original, patched, "EnsureStateStackEmpty");
			//Prefix(original, patched, "get_Int");
			//Prefix(original, patched, "PopState");
			//Prefix(original, patched, "TryRangeInclusiveWhere");
			//Prefix(original, patched, "PushState", Type.EmptyTypes);
			patched = typeof(Rand_Transpile);
			Transpile(original, patched, "PushState", Type.EmptyTypes);
			Transpile(original, patched, "PopState");
			Transpile(original, patched, "TryRangeInclusiveWhere");

			//ThingOwner<Thing>
			original = typeof(ThingOwner<Thing>);
			patched = typeof(ThingOwnerThing_Transpile);
			Transpile(original, patched, "TryAdd", new Type[] { typeof(Thing), typeof(bool) });
			Transpile(original, patched, "Remove");
			patched = typeof(ThingOwnerThing_Patch);
			//Prefix(original, patched, "TryAdd", new Type[] { typeof(Thing), typeof(bool) });
			//Prefix(original, patched, "Remove");

			//RegionListersUpdater
			original = typeof(RegionListersUpdater);
			patched = typeof(RegionListersUpdater_Patch);
			Prefix(original, patched, "DeregisterInRegions");
			Prefix(original, patched, "RegisterInRegions");

			//ListerThings
			original = typeof(ListerThings);
			//patched = typeof(ListerThings_Patch);
			//Prefix(original, patched, "Remove");
			//Prefix(original, patched, "Add");
			patched = typeof(ListerThings_Transpile);
			Transpile(original, patched, "Remove");
			Transpile(original, patched, "Add");

			//JobMaker
			original = typeof(JobMaker);
			patched = typeof(JobMaker_Patch);
			Prefix(original, patched, "MakeJob", new Type[] { });
			Prefix(original, patched, "ReturnToPool");

			//RegionTraverser
			original = typeof(RegionTraverser);
			patched = typeof(RegionTraverser_Patch);
			Prefix(original, patched, "BreadthFirstTraverse", new Type[] {
				typeof(Region),
				typeof(RegionEntryPredicate),
				typeof(RegionProcessor),
				typeof(int),
				typeof(RegionType)
			});

			//ThinkNode_Priority
			original = typeof(ThinkNode_Priority);
			patched = typeof(ThinkNode_Priority_Patch);
			Prefix(original, patched, "TryIssueJobPackage");

			//ThinkNode_PrioritySorter
			original = typeof(ThinkNode_PrioritySorter);
			patched = typeof(ThinkNode_PrioritySorter_Patch);
			Prefix(original, patched, "TryIssueJobPackage");

			//ThingGrid
			original = typeof(ThingGrid);

			//ThingGrid_Transpile
			//patched = typeof(ThingGrid_Transpile);
			//TranspileGeneric(original, patched, "ThingAt", new Type[] { typeof(IntVec3) });

			patched = typeof(ThingGrid_Patch);
			Prefix(original, patched, "RegisterInCell");
			Prefix(original, patched, "DeregisterInCell");
			Prefix(original, patched, "ThingsAt");
			Prefix(original, patched, "ThingAt", new Type[] { typeof(IntVec3), typeof(ThingCategory) });
			Prefix(original, patched, "ThingAt", new Type[] { typeof(IntVec3), typeof(ThingDef) });

			Type original2 = typeof(ThingGrid);
			Type patched2 = typeof(ThingGrid_Patch);
			MethodInfo originalBuilding_DoorThingAt = original2.GetMethod("ThingAt", bf, null, new Type[] { typeof(IntVec3) }, null);
			MethodInfo originalBuilding_DoorThingAtGeneric = originalBuilding_DoorThingAt.MakeGenericMethod(typeof(Building_Door));
			MethodInfo patchedBuilding_DoorThingAt = patched2.GetMethod("ThingAt_Building_Door");
			HarmonyMethod prefixBuilding_Door = new HarmonyMethod(patchedBuilding_DoorThingAt);
			harmony.Patch(originalBuilding_DoorThingAtGeneric, prefix: prefixBuilding_Door);

			//FloatMenuMakerMap
			original = typeof(FloatMenuMakerMap);
			//patched = typeof(FloatMenuMakerMap_Patch);
			//Prefix(original, patched, "AddHumanlikeOrders");
			patched = typeof(FloatMenuMakerMap_Transpile);
			Transpile(original, patched, "AddHumanlikeOrders");

			//RealtimeMoteList			
			original = typeof(RealtimeMoteList);
			patched = typeof(RealtimeMoteList_Patch);
			Prefix(original, patched, "Clear");
			Prefix(original, patched, "MoteSpawned");
			Prefix(original, patched, "MoteDespawned");
			Prefix(original, patched, "MoteListUpdate");
						
			//RCellFinder			
			original = typeof(RCellFinder);
			patched = typeof(RCellFinder_Patch);
			Prefix(original, patched, "RandomWanderDestFor");

			//GenSpawn
			original = typeof(GenSpawn);
			patched = typeof(GenSpawn_Patch);
			Prefix(original, patched, "WipeExistingThings");
			Prefix(original, patched, "CheckMoveItemsAside");

			//PawnDestinationReservationManager
			original = typeof(PawnDestinationReservationManager);
			patched = typeof(Verse_PawnDestinationReservationManager_Patch);
			Prefix(original, patched, "GetPawnDestinationSetFor");
			Prefix(original, patched, "IsReserved", new Type[] { typeof(IntVec3), typeof(Pawn).MakeByRefType() });
			Prefix(original, patched, "Notify_FactionRemoved");
			Prefix(original, patched, "DebugDrawReservations");
			Prefix(original, patched, "Reserve");
			Prefix(original, patched, "ObsoleteAllClaimedBy");
			Prefix(original, patched, "ReleaseAllObsoleteClaimedBy");
			Prefix(original, patched, "ReleaseAllClaimedBy");
			Prefix(original, patched, "ReleaseClaimedBy");
			Prefix(original, patched, "CanReserve");
			Prefix(original, patched, "FirstObsoleteReservationFor");


			//DynamicDrawManager
			original = typeof(DynamicDrawManager);
			patched = typeof(Verse_DynamicDrawManager_Patch);
			Prefix(original, patched, "RegisterDrawable");
			Prefix(original, patched, "DeRegisterDrawable");
			//Prefix(original, patched, "DrawDynamicThings");
			Prefix(original, patched, "LogDynamicDrawThings");

			//Reachability - needs code rewrite - not efficient
			original = typeof(Reachability);
			patched = typeof(Reachability_Patch);
			Prefix(original, patched, "CanReach", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(TraverseParms) });

			//PhysicalInteractionReservationManager
			original = typeof(PhysicalInteractionReservationManager);
			patched = typeof(PhysicalInteractionReservationManager_Patch);
			Prefix(original, patched, "IsReservedBy");
			Prefix(original, patched, "Reserve");
			Prefix(original, patched, "Release");
			Prefix(original, patched, "FirstReserverOf");
			Prefix(original, patched, "FirstReservationFor");
			Prefix(original, patched, "ReleaseAllForTarget");
			Prefix(original, patched, "ReleaseClaimedBy");
			Prefix(original, patched, "ReleaseAllClaimedBy");

			//JobDriver_Wait
			original = typeof(JobDriver_Wait);
			patched = typeof(JobDriver_Wait_Transpile);
			Transpile(original, patched, "CheckForAutoAttack");

			//SelfDefenseUtility
			original = typeof(SelfDefenseUtility);
			patched = typeof(SelfDefenseUtility_Patch);
			Prefix(original, patched, "ShouldStartFleeing");

			//GenClosest
			original = typeof(GenClosest);
			patched = typeof(GenClosest_Patch);
			Prefix(original, patched, "RegionwiseBFSWorker");
			Prefix(original, patched, "ClosestThingReachable");
			Prefix(original, patched, "ClosestThing_Global");

			//PawnUtility
			original = typeof(PawnUtility);
			patched = typeof(PawnUtility_Patch);
			Prefix(original, patched, "PawnBlockingPathAt");
			Prefix(original, patched, "EnemiesAreNearby");
			Prefix(original, patched, "ForceWait");

			//CellFinder
			original = typeof(CellFinder);
			patched = typeof(CellFinder_Patch);
			Prefix(original, patched, "TryFindRandomCellInRegion");
			Prefix(original, patched, "TryFindRandomReachableCellNear");
			Prefix(original, patched, "TryFindBestPawnStandCell");
			Prefix(original, patched, "TryFindRandomCellNear");
			
			//ThingOwnerUtility
			original = typeof(ThingOwnerUtility);
			patched = typeof(ThingOwnerUtility_Patch);
			Prefix(original, patched, "AppendThingHoldersFromThings");
			Prefix(original, patched, "GetAllThingsRecursively", new Type[] { typeof(IThingHolder), typeof(List<Thing>), typeof(bool), typeof(Predicate<IThingHolder>) });

			MethodInfo[] methods = original.GetMethods();

			//MethodInfo originalPawnGetAllThings = original.GetMethod("GetAllThingsRecursively", bf, null, new Type[] { 
			//	typeof(Map), typeof(ThingRequest), typeof(List<Pawn>), typeof(bool), typeof(Predicate<IThingHolder>), typeof(bool) }, null);
			MethodInfo originalPawnGetAllThings = methods[17];
			MethodInfo originalPawnGetAllThingsGeneric = originalPawnGetAllThings.MakeGenericMethod(new Type[] { typeof(Pawn) });
			MethodInfo patchedPawnGetAllThings = patched.GetMethod("GetAllThingsRecursively_Pawn");
			HarmonyMethod prefixPawnGetAllThings = new HarmonyMethod(patchedPawnGetAllThings);
			harmony.Patch(originalPawnGetAllThingsGeneric, prefix: prefixPawnGetAllThings);

			MethodInfo originalThingGetAllThings = methods[17];
			MethodInfo originalThingGetAllThingsGeneric = originalThingGetAllThings.MakeGenericMethod(new Type[] { typeof(Thing) });
			MethodInfo patchedThingGetAllThings = patched.GetMethod("GetAllThingsRecursively_Thing");
			HarmonyMethod prefixThingGetAllThings = new HarmonyMethod(patchedThingGetAllThings);
			harmony.Patch(originalThingGetAllThingsGeneric, prefix: prefixThingGetAllThings);

			//Pawn_MeleeVerbs
			original = typeof(Pawn_MeleeVerbs);
			patched = typeof(Pawn_MeleeVerbs_Patch);
			Prefix(original, patched, "GetUpdatedAvailableVerbsList");

			//AutoUndrafter
			original = typeof(AutoUndrafter);
			patched = typeof(AutoUndrafter_Patch);
			Prefix(original, patched, "AnyHostilePreventingAutoUndraft");

			//AttackTargetsCache
			original = typeof(AttackTargetsCache);
			patched = typeof(AttackTargetsCache_Patch);
			Prefix(original, patched, "GetPotentialTargetsFor");
			Prefix(original, patched, "RegisterTarget");
			Prefix(original, patched, "DeregisterTarget");

			//PawnsFinder
			original = typeof(PawnsFinder);
			patched = typeof(PawnsFinder_Patch);
			Prefix(original, patched, "get_AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists");
			Prefix(original, patched, "get_AllMapsCaravansAndTravelingTransportPods_Alive_Colonists");
			Prefix(original, patched, "get_AllMapsWorldAndTemporary_Alive");

			//PawnDiedOrDownedThoughtsUtility
			original = typeof(PawnDiedOrDownedThoughtsUtility);
			patched = typeof(PawnDiedOrDownedThoughtsUtility_Patch);
			Prefix(original, patched, "RemoveLostThoughts");
			Prefix(original, patched, "RemoveDiedThoughts");

			//AttackTargetFinder
			original = typeof(AttackTargetFinder);
			patched = typeof(AttackTargetFinder_Transpile);
			Transpile(original, patched, "BestAttackTarget");
			Transpile(original, patched, "CanSee");

			patched = typeof(AttackTargetFinder_Patch);
			Prefix(original, patched, "GetRandomShootingTargetByScore");
			//Prefix(original, patched, "BestAttackTarget");
			//Prefix(original, patched, "CanSee");

			//ShootLeanUtility
			original = typeof(ShootLeanUtility);
			patched = typeof(ShootLeanUtility_Patch);
			Prefix(original, patched, "LeanShootingSourcesFromTo");

			//BuildableDef
			original = typeof(BuildableDef);
			patched = typeof(BuildableDef_Transpile);
			Transpile(original, patched, "ForceAllowPlaceOver");
			patched = typeof(BuildableDef_Patch);
			Prefix(original, patched, "get_PlaceWorkers");


			//SustainerManager			
			original = typeof(SustainerManager);
			patched = typeof(SustainerManager_Patch);
			//Prefix(original, patched, "get_AllSustainers");
			Prefix(original, patched, "RegisterSustainer");
			Prefix(original, patched, "DeregisterSustainer");
			Prefix(original, patched, "SustainerManagerUpdate");
			Prefix(original, patched, "UpdateAllSustainerScopes");
			Prefix(original, patched, "SustainerExists");
			Prefix(original, patched, "EndAllInMap");

			//AudioSourceMaker			
			original = typeof(AudioSourceMaker);
			patched = typeof(AudioSourceMaker_Patch);
			Prefix(original, patched, "NewAudioSourceOn");

			//SampleSustainer			
			original = typeof(SampleSustainer);
			patched = typeof(SampleSustainer_Patch);
			Prefix(original, patched, "TryMakeAndPlay");

			//RecreateMapSustainers
			original = typeof(AmbientSoundManager);
			patched = typeof(AmbientSoundManager_Patch);
			//Prefix(original, patched, "RecreateMapSustainers");
			Prefix(original, patched, "EnsureWorldAmbientSoundCreated");

			//SubSustainer
			original = typeof(SubSustainer);
			patched = typeof(SubSustainer_Patch);
			//Prefix(original, patched, "StartSample");
			Prefix(original, patched, "SubSustainerUpdate");

			//SoundStarter
			original = typeof(SoundStarter);
			patched = typeof(SoundStarter_Patch);
			Prefix(original, patched, "PlayOneShot");
			Prefix(original, patched, "PlayOneShotOnCamera");
			//Prefix(original, patched, "TrySpawnSustainer");
			
			//Pawn_RelationsTracker			
			original = typeof(Pawn_RelationsTracker);
			patched = typeof(Pawn_RelationsTracker_Patch);
			Prefix(original, patched, "get_FamilyByBlood");

			//TickManager			
			original = typeof(TickManager);
			patched = typeof(TickManager_Patch);
			Prefix(original, patched, "get_TickRateMultiplier");

			//Battle			
			original = typeof(Battle);
			patched = typeof(Battle_Patch);
			Prefix(original, patched, "ExposeData");
			Prefix(original, patched, "Absorb");

			//Building_Door			
			original = typeof(Building_Door);
			patched = typeof(Building_Door_Patch);
			Prefix(original, patched, "get_DoorPowerOn");
			patched = typeof(Building_Door_Transpile);
			Transpile(original, patched, "get_BlockedOpenMomentary");

			//ThoughtHandler						
			original = typeof(ThoughtHandler);
			patched = typeof(ThoughtHandler_Patch);
			Prefix(original, patched, "TotalOpinionOffset");
			Prefix(original, patched, "MoodOffsetOfGroup");
			Prefix(original, patched, "TotalMoodOffset");
			Prefix(original, patched, "OpinionOffsetOfGroup");

			//FireUtility			
			original = typeof(FireUtility);
			patched = typeof(FireUtility_Patch);
			Prefix(original, patched, "ContainsStaticFire");

			//Fire			
			original = typeof(Fire);
			patched = typeof(Fire_Transpile);
			Transpile(original, patched, "DoComplexCalcs");
			//patched = typeof(Fire_Patch);
			//Prefix(original, patched, "DoComplexCalcs");
			//Prefix(original, patched, "Tick");

			//Projectile			
			original = typeof(Projectile);
			patched = typeof(Projectile_Patch);
			Prefix(original, patched, "ImpactSomething");
			Prefix(original, patched, "Tick");

			//GenGrid_Patch			
			original = typeof(GenGrid);
			patched = typeof(GenGrid_Patch);
			Prefix(original, patched, "InBounds", new Type[] { typeof(IntVec3), typeof(Map) });
			Prefix(original, patched, "InBounds", new Type[] { typeof(Vector3), typeof(Map) });

			//Explosion
			original = typeof(Explosion);
			patched = typeof(Explosion_Patch);
			Prefix(original, patched, "Tick");

			//AttackTargetReservationManager
			original = typeof(AttackTargetReservationManager);
			patched = typeof(AttackTargetReservationManager_Patch);
			Prefix(original, patched, "FirstReservationFor");
			Prefix(original, patched, "ReleaseClaimedBy");
			//Prefix(original, patched, "IsReservedBy");
			Prefix(original, patched, "CanReserve");
			Prefix(original, patched, "ReleaseAllForTarget");
			Prefix(original, patched, "ReleaseAllClaimedBy");
			patched = typeof(AttackTargetReservationManager_Transpile);
			Transpile(original, patched, "IsReservedBy");


			//PawnCollisionTweenerUtility
			original = typeof(PawnCollisionTweenerUtility);
			patched = typeof(PawnCollisionTweenerUtility_Patch);
			Prefix(original, patched, "GetPawnsStandingAtOrAboutToStandAt");
			Prefix(original, patched, "CanGoDirectlyToNextCell");

			//GridsUtility			
			original = typeof(GridsUtility);
			patched = typeof(GridsUtility_Patch);
			Prefix(original, patched, "GetTerrain");
			Prefix(original, patched, "IsInPrisonCell");
			patched = typeof(GridsUtility_Transpile);
			Transpile(original, patched, "GetGas");

			//ReservationManager
			original = typeof(ReservationManager);
			patched = typeof(ReservationManager_Patch);
			Prefix(original, patched, "ReleaseClaimedBy");
			Prefix(original, patched, "Reserve");
			Prefix(original, patched, "Release");
			Prefix(original, patched, "FirstReservationFor");
			Prefix(original, patched, "IsReservedByAnyoneOf");
			Prefix(original, patched, "FirstRespectedReserver");
			patched = typeof(ReservationManager_Transpile);
			Transpile(original, patched, "CanReserve");

			//FloodFiller - inefficient global lock			
			original = typeof(FloodFiller);
			patched = typeof(FloodFiller_Patch);
			Prefix(original, patched, "FloodFill", new Type[] { typeof(IntVec3), typeof(Predicate<IntVec3>), typeof(Func<IntVec3, int, bool>), typeof(int), typeof(bool), typeof(IEnumerable<IntVec3>) });

			//Verb
			original = typeof(Verb);
			//patched = typeof(Verb_Patch);
			//Prefix(original, patched, "CanHitFromCellIgnoringRange");
			//Prefix(original, patched, "TryFindShootLineFromTo");
			patched = typeof(Verb_Transpile);
			Transpile(original, patched, "TryFindShootLineFromTo");
			Transpile(original, patched, "CanHitFromCellIgnoringRange");

			//FastPriorityQueue<KeyValuePair<IntVec3, float>>
			original = typeof(FastPriorityQueue<KeyValuePair<IntVec3, float>>);
			patched = typeof(FastPriorityQueueKeyValuePairIntVec3Float_Patch);
			Prefix(original, patched, "Push");
			Prefix(original, patched, "Pop");
			Prefix(original, patched, "Clear");
			//Prefix(original, patched, "SwapElements");
			//Prefix(original, patched, "CompareElements");

			//MapPawns
			original = typeof(MapPawns);
			patched = typeof(MapPawns_Patch);
			Prefix(original, patched, "get_AllPawns");
			Prefix(original, patched, "LogListedPawns");
			Prefix(original, patched, "get_AnyPawnBlockingMapRemoval");
			Prefix(original, patched, "get_FreeColonistsSpawnedOrInPlayerEjectablePodsCount");
			Prefix(original, patched, "FreeHumanlikesSpawnedOfFaction");
			Prefix(original, patched, "get_AllPawnsUnspawned");
			Prefix(original, patched, "get_SpawnedPawnsWithAnyHediff");
			Prefix(original, patched, "PawnsInFaction");
			//Prefix(original, patched, "SpawnedPawnsInFaction");
			//Prefix(original, patched, "RegisterPawn");
			//Prefix(original, patched, "DeRegisterPawn");
			patched = typeof(MapPawns_Transpile);
			Transpile(original, patched, "RegisterPawn");
			Transpile(original, patched, "DeRegisterPawn");

			//MapTemperatures
			original = typeof(MapTemperature);
			patched = typeof(MapTemperature_Patch);
			Prefix(original, patched, "MapTemperatureTick");

			//Region
			original = typeof(Region);
			patched = typeof(Region_Patch);
			Prefix(original, patched, "DangerFor");
			Prefix(original, patched, "get_AnyCell");

			//Pawn_WorkSettings
			original = typeof(Pawn_WorkSettings);
			//patched = typeof(Pawn_WorkSettings_Patch);
			//Prefix(original, patched, "CacheWorkGiversInOrder");
			patched = typeof(Pawn_WorkSettings_Transpile);
			Transpile(original, patched, "CacheWorkGiversInOrder");

			//Sample
			original = typeof(Sample);
			patched = typeof(Sample_Patch);
			Prefix(original, patched, "Update");

			//Sustainer
			original = typeof(Sustainer);
			patched = typeof(Sustainer_Patch);
			Prefix(original, patched, "Cleanup");

			//ImmunityHandler
			original = typeof(ImmunityHandler);
			patched = typeof(ImmunityHandler_Patch);
			Prefix(original, patched, "ImmunityHandlerTick"); 
			Prefix(original, patched, "NeededImmunitiesNow");

			//Room
			original = typeof(Room);
			patched = typeof(Room_Patch);
			Prefix(original, patched, "OpenRoofCountStopAt");
			Prefix(original, patched, "get_PsychologicallyOutdoors");
			Prefix(original, patched, "RemoveRegion");
			Prefix(original, patched, "Notify_RoofChanged");
			Prefix(original, patched, "Notify_RoomShapeOrContainedBedsChanged");
			patched = typeof(Room_Transpile);
			//Transpile(original, patched, "RemoveRegion");


			//LongEventHandler
			original = typeof(LongEventHandler);
			patched = typeof(LongEventHandler_Patch);
			Prefix(original, patched, "ExecuteToExecuteWhenFinished");

			//SituationalThoughtHandler
			original = typeof(SituationalThoughtHandler);
			patched = typeof(SituationalThoughtHandler_Patch);
			Prefix(original, patched, "AppendSocialThoughts");
			Prefix(original, patched, "Notify_SituationalThoughtsDirty");
			Prefix(original, patched, "RemoveExpiredThoughtsFromCache");
			ConstructorInfo constructorMethod3 = original.GetConstructor(new Type[] { typeof(Pawn) });
			MethodInfo cpMethod3 = patched.GetMethod("Postfix_Constructor");
			harmony.Patch(constructorMethod3, postfix: new HarmonyMethod(cpMethod3));

			//GenAdjFast
			original = typeof(GenAdjFast);
			patched = typeof(GenAdjFast_Patch);
			Prefix(original, patched, "AdjacentCells8Way", new Type[] { typeof(IntVec3) });
			Prefix(original, patched, "AdjacentCells8Way", new Type[] { typeof(IntVec3), typeof(Rot4), typeof(IntVec2) });
			Prefix(original, patched, "AdjacentCellsCardinal", new Type[] { typeof(IntVec3) });
			Prefix(original, patched, "AdjacentCellsCardinal", new Type[] { typeof(IntVec3), typeof(Rot4), typeof(IntVec2) });

			//GenAdj
			original = typeof(GenAdj);
			patched = typeof(GenAdj_Patch);
			Prefix(original, patched, "TryFindRandomAdjacentCell8WayWithRoomGroup", new Type[] {
				typeof(IntVec3), typeof(Rot4), typeof(IntVec2), typeof(Map), typeof(IntVec3).MakeByRefType() });

			//LordToil_Siege
			original = typeof(LordToil_Siege);
			patched = typeof(LordToil_Siege_Patch);
			Prefix(original, patched, "UpdateAllDuties");

			//BattleLog
			original = typeof(BattleLog);
			//patched = typeof(BattleLog_Patch);
			//Prefix(original, patched, "Add");
			patched = typeof(BattleLog_Transpile);
			Transpile(original, patched, "Add");

			//PawnCapacitiesHandler
			original = typeof(PawnCapacitiesHandler);
			patched = typeof(PawnCapacitiesHandler_Patch);
			Prefix(original, patched, "GetLevel");
			Prefix(original, patched, "Notify_CapacityLevelsDirty");
			Prefix(original, patched, "Clear");
			ConstructorInfo constructorMethod = original.GetConstructor(new Type[] { typeof(Pawn) });
			MethodInfo cpMethod = patched.GetMethod("Postfix_Constructor");
			harmony.Patch(constructorMethod, postfix: new HarmonyMethod(cpMethod));
			
			//PathFinder
			original = typeof(PathFinder);
			patched = typeof(PathFinder_Transpile);
			Transpile(original, patched, "FindPath", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode) });
			//patched = typeof(PathFinder_Patch);
			//Prefix(original, patched, "FindPath", new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode) });


			//WorldPawns
			original = typeof(WorldPawns);
			patched = typeof(WorldPawns_Patch);
			Prefix(original, patched, "get_AllPawnsAlive");

			//PawnPath
			original = typeof(PawnPath);
			patched = typeof(PawnPath_Patch);
			Prefix(original, patched, "AddNode");
			Prefix(original, patched, "ReleaseToPool");

			//GenCollection
			original = typeof(GenCollection);
			patched = typeof(GenCollection_Patch);
			MethodInfo[] genCollectionMethods = original.GetMethods();
			MethodInfo originalRemoveAll = null;
			foreach (MethodInfo mi in genCollectionMethods)
            {
				if(mi.Name.Equals("RemoveAll") && mi.GetGenericArguments().Length == 2)
                {
					originalRemoveAll = mi;
					break;
				}
            }
			//public static int RemoveAll<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Predicate<KeyValuePair<TKey, TValue>> predicate)
			//Type pType = typeof(Predicate<>).MakeGenericType(new Type[] { typeof(KeyValuePair<,>) });
			//originalRemoveAll = AccessTools.Method(original, "RemoveAll", new Type[] { typeof(Dictionary<,>), pType });

			MethodInfo originalRemoveAllGeneric = originalRemoveAll.MakeGenericMethod(new Type[] { typeof(object), typeof(object) });
			MethodInfo patchedRemoveAll = patched.GetMethod("RemoveAll_Object_Object_Patch");
			HarmonyMethod prefixRemoveAll = new HarmonyMethod(patchedRemoveAll);
			harmony.Patch(originalRemoveAllGeneric, prefix: prefixRemoveAll);
			//patched = typeof(GenCollection_Transpile);
			//MethodInfo transpileRemoveAll = patched.GetMethod("RemoveAll");
			//HarmonyMethod harmonyRemoveAll = new HarmonyMethod(transpileRemoveAll);
			//harmony.Patch(originalRemoveAll, transpiler: harmonyRemoveAll);

			//SoundSizeAggregator
			original = typeof(SoundSizeAggregator);
			patched = typeof(SoundSizeAggregator_Patch);
			Prefix(original, patched, "RegisterReporter");
			Prefix(original, patched, "RemoveReporter");

			//HediffSet
			original = typeof(HediffSet);
			patched = typeof(HediffSet_Transpile);
			Transpile(original, patched, "PartIsMissing");
			Transpile(original, patched, "HasDirectlyAddedPartFor");
			Transpile(original, patched, "AddDirect");
			patched = typeof(HediffSet_Patch);
			Prefix(original, patched, "CacheMissingPartsCommonAncestors");

			//LanguageWordInfo
			original = typeof(LanguageWordInfo);
			patched = typeof(LanguageWordInfo_Patch);
			Prefix(original, patched, "TryResolveGender");

			//JobGiver_ConfigurableHostilityResponse
			original = typeof(JobGiver_ConfigurableHostilityResponse);
			patched = typeof(JobGiver_ConfigurableHostilityResponse_Patch);
			Prefix(original, patched, "TryGetFleeJob");


			//Pawn_InteractionsTracker
			original = typeof(Pawn_InteractionsTracker);
			//patched = typeof(Pawn_InteractionsTracker_Patch);
			//Prefix(original, patched, "TryInteractRandomly");
			patched = typeof(Pawn_InteractionsTracker_Transpile);
			Transpile(original, patched, "TryInteractRandomly");

			//Toils_Ingest
			original = typeof(Toils_Ingest);
			patched = typeof(Toils_Ingest_Patch);
			Prefix(original, patched, "TryFindAdjacentIngestionPlaceSpot");

			//BeautyUtility
			original = typeof(BeautyUtility);
			patched = typeof(BeautyUtility_Patch);
			Prefix(original, patched, "AverageBeautyPerceptible");

			//FoodUtility
			original = typeof(FoodUtility);
			patched = typeof(FoodUtility_Transpile);
			Transpile(original, patched, "FoodOptimality");

			//TendUtility
			original = typeof(TendUtility);
			patched = typeof(TendUtility_Patch);
			Prefix(original, patched, "GetOptimalHediffsToTendWithSingleTreatment");

			//WanderUtility
			original = typeof(WanderUtility);
			patched = typeof(WanderUtility_Patch);
			Prefix(original, patched, "GetColonyWanderRoot");

			//RegionAndRoomUpdater
			original = typeof(RegionAndRoomUpdater);
			patched = typeof(RegionAndRoomUpdater_Patch);
			Prefix(original, patched, "FloodAndSetRoomGroups");
			Prefix(original, patched, "CombineNewAndReusedRoomsIntoContiguousGroups");
			Prefix(original, patched, "TryRebuildDirtyRegionsAndRooms");
			
			//GenRadial
			original = typeof(GenRadial);
			patched = typeof(GenRadial_Transpile);
			Transpile(original, patched, "ProcessEquidistantCells");

			//WealthWatcher
			original = typeof(WealthWatcher);
			patched = typeof(WealthWatcher_Patch);
			Prefix(original, patched, "ForceRecount");

			//Medicine
			original = typeof(Medicine);
			patched = typeof(Medicine_Patch);
			Prefix(original, patched, "GetMedicineCountToFullyHeal");

			//WorkGiver_DoBill
			original = typeof(WorkGiver_DoBill);
			patched = typeof(WorkGiver_DoBill_Patch);
			Prefix(original, patched, "TryFindBestBillIngredients");

			//JobGiver_Work
			original = typeof(JobGiver_Work);
			patched = typeof(JobGiver_Work_Patch);
			//Prefix(original, patched, "TryIssueJobPackage");

			//ThingCountUtility
			original = typeof(ThingCountUtility);
			patched = typeof(ThingCountUtility_Patch);
			Prefix(original, patched, "AddToList");

			//WorkGiver_ConstructDeliverResources
			original = typeof(WorkGiver_ConstructDeliverResources);
			//patched = typeof(WorkGiver_ConstructDeliverResources_Patch);
			patched = typeof(WorkGiver_ConstructDeliverResources_Transpile);
			Transpile(original, patched, "ResourceDeliverJobFor", new string[] { "CodeOptimist.JobsOfOpportunity" });

			//GenText
			original = typeof(GenText);
			patched = typeof(GenText_Patch);
			Prefix(original, patched, "CapitalizeSentences");

			//BiomeDef
			original = typeof(BiomeDef);
			patched = typeof(BiomeDef_Patch);
			Prefix(original, patched, "CachePlantCommonalitiesIfShould");
			Prefix(original, patched, "get_LowestWildAndCavePlantOrder");

			//WildPlantSpawner
			original = typeof(WildPlantSpawner);
			patched = typeof(WildPlantSpawner_Patch);
			Prefix(original, patched, "CheckSpawnWildPlantAt");

			//TileTemperaturesComp
			original = typeof(TileTemperaturesComp);
			patched = typeof(TileTemperaturesComp_Transpile);
			Transpile(original, patched, "WorldComponentTick");

			//PawnRelationUtility
			original = typeof(PawnRelationUtility);
			patched = typeof(PawnRelationUtility_Patch);
			Prefix(original, patched, "GetMostImportantColonyRelative");

			//SustainerAggregatorUtility
			original = typeof(SustainerAggregatorUtility);
			patched = typeof(SustainerAggregatorUtility_Patch);
			Prefix(original, patched, "AggregateOrSpawnSustainerFor");

			//StoryState
			original = typeof(StoryState);
			patched = typeof(StoryState_Patch);
			Prefix(original, patched, "RecordPopulationIncrease");

			//GrammarResolverSimple
			original = typeof(GrammarResolverSimple);
			patched = typeof(GrammarResolverSimple_Transpile);
			Transpile(original, patched, "Formatted");

			//GrammarResolver
			original = typeof(GrammarResolver);
			patched = typeof(GrammarResolver_Transpile);
			Transpile(original, patched, "AddRule");

			//JobQueue
			original = typeof(JobQueue);
			patched = typeof(JobQueue_Patch);
			Prefix(original, patched, "AnyCanBeginNow");
			Prefix(original, patched, "EnqueueFirst");
			Prefix(original, patched, "EnqueueLast");
			Prefix(original, patched, "Contains");
			Prefix(original, patched, "Extract");
			Prefix(original, patched, "Dequeue");

			//MeditationFocusTypeAvailabilityCache
			original = typeof(MeditationFocusTypeAvailabilityCache);
			patched = typeof(MeditationFocusTypeAvailabilityCache_Patch);
			Prefix(original, patched, "PawnCanUse");
			Prefix(original, patched, "ClearFor");

			//LightningBoltMeshMaker
			original = typeof(LightningBoltMeshMaker);
			patched = typeof(LightningBoltMeshMaker_Patch);
			Prefix(original, patched, "NewBoltMesh");

			//Unforce Normal Speed			
			original = typeof(TimeControls);
			patched = typeof(TimeControls_Patch);
			Prefix(original, patched, "DoTimeControlsGUI");

			//PathGrid
			original = typeof(PathGrid);
			patched = typeof(PathGrid_Patch);
			Prefix(original, patched, "CalculatedCostAt");

			//GlobalControlsUtility
			original = typeof(GlobalControlsUtility);
			patched = typeof(GlobalControlsUtility_Patch);
			Postfix(original, patched, "DoTimespeedControls");

			//InfestationCellFinder
			original = typeof(InfestationCellFinder);
			patched = typeof(InfestationCellFinder_Patch);
			Prefix(original, patched, "CalculateDistanceToColonyBuildingGrid");

			//RegionCostCalculator
			original = typeof(RegionCostCalculator);
			patched = typeof(RegionCostCalculator_Patch);
			Prefix(original, patched, "GetPreciseRegionLinkDistances");

			//EditWindow_Log
			//original = typeof(EditWindow_Log);
			//patched = typeof(EditWindow_Log_Patch);
			//Prefix(original, patched, "DoMessagesListing");

			//GUIStyle
			original = typeof(GUIStyle);
			patched = typeof(GUIStyle_Patch);
			Prefix(original, patched, "CalcHeight");
			Prefix(original, patched, "CalcSize");

			//WorldGrid
			original = typeof(WorldGrid);
			patched = typeof(WorldGrid_Patch);
			Prefix(original, patched, "IsNeighbor");

			//ReservationUtility
			original = typeof(ReservationUtility);
			patched = typeof(ReservationUtility_Patch);
			Prefix(original, patched, "CanReserve");

			//WorldFloodFiller
			original = typeof(WorldFloodFiller);
			patched = typeof(WorldFloodFiller_Patch);
			Prefix(original, patched, "FloodFill", new Type[] { typeof(int), typeof(Predicate<int>) , typeof(Func<int, int, bool>) , typeof(int) , typeof(IEnumerable<int>) });

			//RecipeWorkerCounter
			original = typeof(RecipeWorkerCounter);
			patched = typeof(RecipeWorkerCounter_Patch);
			Prefix(original, patched, "GetCarriedCount");

			//Pawn_RotationTracker
			original = typeof(Pawn_RotationTracker);
			patched = typeof(Pawn_RotationTracker_Patch);
			Prefix(original, patched, "UpdateRotation");

			//PERFORMANCE IMPROVEMENTS

			//HediffGiver_Heat
			original = typeof(HediffGiver_Heat);
			patched = typeof(HediffGiver_Heat_Patch);
			Prefix(original, patched, "OnIntervalPassed");

			//HediffGiver_Hypothermia
			original = typeof(HediffGiver_Hypothermia);
			//patched = typeof(HediffGiver_Hypothermia_Patch);
			//Prefix(original, patched, "OnIntervalPassed");
			patched = typeof(HediffGiver_Hypothermia_Transpile);
			Transpile(original, patched, "OnIntervalPassed");

			//Pawn_MindState - hack for speedup. replaced (GenLocalDate.DayTick((Thing)__instance.pawn) interactions today with always 0
			original = typeof(Pawn_MindState);
			patched = typeof(Pawn_MindState_Patch);
			Prefix(original, patched, "MindStateTick");
			
			//WorldObjectsHolder
			original = typeof(WorldObjectsHolder);
			patched = typeof(WorldObjectsHolder_Patch);
			Prefix(original, patched, "WorldObjectsHolderTick");

			//WorldPawns
			original = typeof(WorldPawns);
			patched = typeof(WorldPawns_Patch);
			Prefix(original, patched, "WorldPawnsTick");

			//SteadyEnvironmentEffects
			original = typeof(SteadyEnvironmentEffects);
			patched = typeof(SteadyEnvironmentEffects_Patch);
			Prefix(original, patched, "SteadyEnvironmentEffectsTick");

			//WindManager
			original = typeof(WindManager);
			patched = typeof(WindManager_Patch);
			Prefix(original, patched, "WindManagerTick");

			//FactionManager
			original = typeof(FactionManager);
			patched = typeof(FactionManager_Patch);
			Prefix(original, patched, "FactionManagerTick");			

			//WildPlantSpawner
			original = typeof(WildPlantSpawner);
			patched = typeof(WildPlantSpawner_Patch);
			Prefix(original, patched, "WildPlantSpawnerTickInternal");

			//SeasonUtility
			original = typeof(SeasonUtility);
			patched = typeof(SeasonUtility_Patch);
			Prefix(original, patched, "GetReportedSeason");
			
			//TradeShip
			original = typeof(TradeShip);
			patched = typeof(TradeShip_Patch);
			Prefix(original, patched, "PassingShipTick");

			//DateNotifier
			original = typeof(DateNotifier);
			patched = typeof(DateNotifier_Patch);
			Prefix(original, patched, "FindPlayerHomeWithMinTimezone");

			//GenTemperature			
			original = typeof(GenTemperature);
			patched = typeof(GenTemperature_Patch);
			Prefix(original, patched, "GetTemperatureFromSeasonAtTile");
			Prefix(original, patched, "SeasonalShiftAmplitudeAt");
			Prefix(original, patched, "EqualizeTemperaturesThroughBuilding");
			Prefix(original, patched, "PushHeat", new Type[] { typeof(IntVec3), typeof(Map), typeof(float) });

			//WorldComponentUtility			
			original = typeof(WorldComponentUtility);
			patched = typeof(WorldComponentUtility_Patch);
			Prefix(original, patched, "WorldComponentTick");

			//TickManager			
			original = typeof(TickManager);
			patched = typeof(TickManager_Patch);
			Prefix(original, patched, "DoSingleTick");

			//Map			
			original = typeof(Map);
			patched = typeof(Map_Patch);
			//Prefix(original, patched, "MapUpdate");
			patched = typeof(Map_Transpile);
			Transpile(original, patched, "MapUpdate");

			//ThinkNode_SubtreesByTag			
			original = typeof(ThinkNode_SubtreesByTag);
			patched = typeof(ThinkNode_SubtreesByTag_Patch);
			Prefix(original, patched, "TryIssueJobPackage");

			//ThinkNode_QueuedJob			
			original = typeof(ThinkNode_QueuedJob);
			patched = typeof(ThinkNode_QueuedJob_Patch);
			Prefix(original, patched, "TryIssueJobPackage");
			
			//JobDriver			
			original = typeof(JobDriver);
			patched = typeof(JobDriver_Patch);
			Prefix(original, patched, "TryActuallyStartNextToil");

			//TemperatureCache			
			original = typeof(TemperatureCache);
			patched = typeof(TemperatureCache_Patch);
			Prefix(original, patched, "TryCacheRegionTempInfo");

			//JobGiver_AnimalFlee			
			original = typeof(JobGiver_AnimalFlee);
			patched = typeof(JobGiver_AnimalFlee_Patch);
			Prefix(original, patched, "FleeLargeFireJob");

			//PlayLog			
			original = typeof(PlayLog);
			patched = typeof(PlayLog_Patch);
			Prefix(original, patched, "RemoveEntry");

			//ResourceCounter			
			original = typeof(ResourceCounter);
			patched = typeof(ResourceCounter_Patch);
			Prefix(original, patched, "get_TotalHumanEdibleNutrition");
			Prefix(original, patched, "ResetDefs");
			Prefix(original, patched, "ResetResourceCounts");
			Prefix(original, patched, "GetCount");
			Prefix(original, patched, "GetCountIn", new Type[] { typeof(ThingRequestGroup) });
			Prefix(original, patched, "UpdateResourceCounts");

			//UniqueIDsManager	
			original = typeof(UniqueIDsManager);
			patched = typeof(UniqueIDsManager_Patch);
			Prefix(original, patched, "GetNextID");

			//MOD COMPATIBILITY

			//MapGenerator (Z-levels)
			original = typeof(MapGenerator);
			patched = typeof(MapGenerator_Patch);
			Prefix(original, patched, "GenerateMap");

			//RenderTexture (Giddy-Up)
			original = typeof(RenderTexture);
			patched = typeof(RenderTexture_Patch);
			//Prefix(original, patched, "GetTemporary", new Type[] { typeof(int), typeof(int), typeof(int), typeof(RenderTextureFormat), typeof(RenderTextureReadWrite) });
			Prefix(original, patched, "GetTemporaryImpl");
			
			//GetTemporary (CE)
			//Prefix(original, patched, "GetTemporary", new Type[] { typeof(int), typeof(int), typeof(int), typeof(RenderTextureFormat), typeof(RenderTextureReadWrite), typeof(int) });
			//Prefix(original, patched, "get_active");
			//Prefix(original, patched, "set_active");
			
			//Graphics (Giddy-Up)
			original = typeof(Graphics);
			patched = typeof(Graphics_Patch);
			Prefix(original, patched, "Blit", new Type[] { typeof(Texture), typeof(RenderTexture) });

			//Graphics (Giddy-Up)
			original = typeof(Texture2D);
			patched = typeof(Texture2D_Patch);
			//Prefix(original, patched, "GetPixel", new Type[] { typeof(int), typeof(int) });
			Prefix(original, patched, "Internal_Create");
			Prefix(original, patched, "ReadPixels", new Type[] { typeof(Rect), typeof(int), typeof(int), typeof(bool) });
			Prefix(original, patched, "Apply", new Type[] { typeof(bool), typeof(bool) });

			//original = typeof(Mesh);
			//patched = typeof(Mesh_Patch);
			//ConstructorInfo constructorMethod4 = original.GetConstructor(Type.EmptyTypes);
			//MethodInfo cpMethod4 = patched.GetMethod("MeshSafe");
			//harmony.Patch(constructorMethod4, prefix: new HarmonyMethod(cpMethod4));

			original = typeof(SectionLayer);
			patched = typeof(SectionLayer_Patch);
			Prefix(original, patched, "GetSubMesh");

			original = typeof(GraphicDatabaseHeadRecords);
			patched = typeof(GraphicDatabaseHeadRecords_Patch);
			Prefix(original, patched, "BuildDatabaseIfNecessary");

			original = typeof(MeshMakerPlanes);
			patched = typeof(MeshMakerPlanes_Patch);
			Prefix(original, patched, "NewPlaneMesh", new Type[] { typeof(Vector2), typeof(bool), typeof(bool), typeof(bool) });

			original = typeof(MeshMakerShadows);
			patched = typeof(MeshMakerShadows_Patch);
			Prefix(original, patched, "NewShadowMesh", new Type[] { typeof(float), typeof(float), typeof(float) });


			giddyUpCoreStorageExtendedPawnData = AccessTools.TypeByName("GiddyUpCore.Storage.ExtendedPawnData");
			giddyUpCoreJobsGUC_JobDefOf = AccessTools.TypeByName("GiddyUpCore.Jobs.GUC_JobDefOf");
			giddyUpCoreUtilitiesTextureUtility = AccessTools.TypeByName("GiddyUpCore.Utilities.TextureUtility");
			giddyUpCoreStorageExtendedDataStorage = AccessTools.TypeByName("GiddyUpCore.Storage.ExtendedDataStorage");
			giddyUpCoreJobsJobDriver_Mounted = AccessTools.TypeByName("GiddyUpCore.Jobs.JobDriver_Mounted");
			giddyUpCoreHarmonyPawnJobTracker_DetermineNextJob = AccessTools.TypeByName("GiddyUpCore.Harmony.Pawn_JobTracker_DetermineNextJob");
			hospitalityCompUtility = AccessTools.TypeByName("Hospitality.CompUtility");
			hospitalityCompGuest = AccessTools.TypeByName("Hospitality.CompGuest");
			awesomeInventoryJobsJobGiver_FindItemByRadius = AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_FindItemByRadius");
			awesomeInventoryErrorMessage = AccessTools.TypeByName("AwesomeInventory.ErrorMessage");
			jobGiver_AwesomeInventory_TakeArm = AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_AwesomeInventory_TakeArm");
			awesomeInventoryJobsJobGiver_FindItemByRadiusSub = AccessTools.TypeByName("AwesomeInventory.Jobs.JobGiver_FindItemByRadius+<>c__DisplayClass17_0");
			pawnRulesPatchRimWorld_Pawn_GuestTracker_SetGuestStatus = AccessTools.TypeByName("PawnRules.Patch.RimWorld_Pawn_GuestTracker_SetGuestStatus");
			combatExtendedCE_Utility = AccessTools.TypeByName("CombatExtended.CE_Utility");
			combatExtendedVerb_LaunchProjectileCE = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
			combatExtendedVerb_MeleeAttackCE = AccessTools.TypeByName("CombatExtended.Verb_MeleeAttackCE");

			if (giddyUpCoreUtilitiesTextureUtility != null)
			{
				string methodName = "setDrawOffset";
				Log.Message("RimThreaded is patching " + giddyUpCoreUtilitiesTextureUtility.FullName + " " + methodName);
				patched = typeof(TextureUtility_Transpile);
				Transpile(giddyUpCoreUtilitiesTextureUtility, patched, methodName);
			}

			if (giddyUpCoreStorageExtendedDataStorage != null)
			{
				string methodName = "DeleteExtendedDataFor";
				Log.Message("RimThreaded is patching " + giddyUpCoreStorageExtendedDataStorage.FullName + " " + methodName);
				patched = typeof(ExtendedDataStorage_Transpile);
				Transpile(giddyUpCoreStorageExtendedDataStorage, patched, methodName);

				methodName = "GetExtendedDataFor";
				Log.Message("RimThreaded is patching " + giddyUpCoreStorageExtendedDataStorage.FullName + " " + methodName);
				Transpile(giddyUpCoreStorageExtendedDataStorage, patched, methodName);
			}

			if (giddyUpCoreHarmonyPawnJobTracker_DetermineNextJob != null)
			{
				string methodName = "Postfix";
				Log.Message("RimThreaded is patching " + giddyUpCoreHarmonyPawnJobTracker_DetermineNextJob.FullName + " " + methodName);
				patched = typeof(Pawn_JobTracker_DetermineNextJob_Transpile);
				Transpile(giddyUpCoreHarmonyPawnJobTracker_DetermineNextJob, patched, methodName);
			}

			if (giddyUpCoreJobsJobDriver_Mounted != null)
            {
				string methodName = "<waitForRider>b__8_0";
				foreach (MethodInfo methodInfo in ((TypeInfo)giddyUpCoreJobsJobDriver_Mounted).DeclaredMethods)
				{
					if (methodInfo.Name.Equals(methodName))
					{
						Log.Message("RimThreaded is patching " + giddyUpCoreJobsJobDriver_Mounted.FullName + " " + methodName);
						patched = typeof(JobDriver_Mounted_Transpile);
						MethodInfo pMethod = patched.GetMethod("WaitForRider");
						harmony.Patch(methodInfo, transpiler: new HarmonyMethod(pMethod));
					}
				}
			}

			if (hospitalityCompUtility != null)
			{
				string methodName = "CompGuest";
				Log.Message("RimThreaded is patching " + hospitalityCompUtility.FullName + " " + methodName);
				patched = typeof(CompUtility_Transpile);
				Transpile(hospitalityCompUtility, patched, methodName);
				methodName = "OnPawnRemoved";
				Log.Message("RimThreaded is patching " + hospitalityCompUtility.FullName + " " + methodName);
				Transpile(hospitalityCompUtility, patched, methodName);
			}

			if (awesomeInventoryJobsJobGiver_FindItemByRadius != null)
			{
				string methodName = "Reset";
				Log.Message("RimThreaded is patching " + awesomeInventoryJobsJobGiver_FindItemByRadius.FullName + " " + methodName);
				patched = typeof(JobGiver_FindItemByRadius_Transpile);
				Transpile(awesomeInventoryJobsJobGiver_FindItemByRadius, patched, methodName);
				methodName = "FindItem";
				Log.Message("RimThreaded is patching " + awesomeInventoryJobsJobGiver_FindItemByRadius.FullName + " " + methodName);
				Transpile(awesomeInventoryJobsJobGiver_FindItemByRadius, patched, methodName);
				//Prefix(awesomeInventoryJobsJobGiver_FindItemByRadius, patched, methodName);
			}

			if (pawnRulesPatchRimWorld_Pawn_GuestTracker_SetGuestStatus != null)
			{
				string methodName = "Prefix";
				Log.Message("RimThreaded is patching " + pawnRulesPatchRimWorld_Pawn_GuestTracker_SetGuestStatus.FullName + " " + methodName);
				patched = typeof(RimWorld_Pawn_GuestTracker_SetGuestStatus_Transpile);
				Transpile(pawnRulesPatchRimWorld_Pawn_GuestTracker_SetGuestStatus, patched, methodName);
			}

			if (combatExtendedCE_Utility != null)
			{
				string methodName = "BlitCrop";
				Log.Message("RimThreaded is patching " + combatExtendedCE_Utility.FullName + " " + methodName);
				patched = typeof(CE_Utility_Transpile);
				Transpile(combatExtendedCE_Utility, patched, methodName);
				methodName = "GetColorSafe";
				Log.Message("RimThreaded is patching " + combatExtendedCE_Utility.FullName + " " + methodName);
				Transpile(combatExtendedCE_Utility, patched, methodName);
			}
			if (combatExtendedVerb_LaunchProjectileCE != null)
			{
				string methodName = "CanHitFromCellIgnoringRange";
				patched = typeof(Verb_LaunchProjectileCE_Transpile);
				Log.Message("RimThreaded is patching " + combatExtendedVerb_LaunchProjectileCE.FullName + " " + methodName);
				Transpile(combatExtendedVerb_LaunchProjectileCE, patched, methodName);
				methodName = "TryFindCEShootLineFromTo";
				Log.Message("RimThreaded is patching " + combatExtendedVerb_LaunchProjectileCE.FullName + " " + methodName);
				Transpile(combatExtendedVerb_LaunchProjectileCE, patched, methodName);
			}
			if (combatExtendedVerb_MeleeAttackCE != null)
			{
				string methodName = "TryCastShot";
				patched = typeof(Verb_MeleeAttackCE_Transpile);
				Log.Message("RimThreaded is patching " + combatExtendedVerb_MeleeAttackCE.FullName + " " + methodName);
				Transpile(combatExtendedVerb_MeleeAttackCE, patched, methodName);
			}

			Log.Message("RimThreaded patching is complete.");
		}

		public static void Prefix(Type original, Type patched, String methodName, Type[] orig_type)
		{
			MethodInfo oMethod = original.GetMethod(methodName, bf, null, orig_type, null);
			Type[] patch_type = new Type[orig_type.Length];
			Array.Copy(orig_type, patch_type, orig_type.Length);

			if (!oMethod.ReturnType.Name.Equals("Void"))
			{
				Type[] temp_type = patch_type;
				patch_type = new Type[temp_type.Length + 1];
				patch_type[0] = oMethod.ReturnType.MakeByRefType();
				Array.Copy(temp_type, 0, patch_type, 1, temp_type.Length);
			}
			if (!oMethod.IsStatic)
			{
				Type[] temp_type = patch_type;
				patch_type = new Type[temp_type.Length + 1];
				patch_type[0] = original;
				Array.Copy(temp_type, 0, patch_type, 1, temp_type.Length);
			}

			MethodInfo pMethod = patched.GetMethod(methodName, patch_type);
			if (null == oMethod)
			{
				Log.Message(original.ToString() + "." + methodName + "(" + string.Join(",", orig_type.Select(x => x.ToString()).ToArray()) + ") not found");
			}
			if (null == pMethod)
			{
				Log.Message(patched.ToString() + "." + methodName + "(" + string.Join(",", patch_type.Select(x => x.ToString()).ToArray()) + ") not found");
			}
			harmony.Patch(oMethod, prefix: new HarmonyMethod(pMethod));
		}
		public static void Prefix(Type original, Type patched, String methodName)
		{
			MethodInfo oMethod = original.GetMethod(methodName, bf);
			MethodInfo pMethod = patched.GetMethod(methodName);
			if (null == oMethod)
			{
				Log.Message(original.ToString() + "." + methodName + " not found");
			}
			if (null == pMethod)
			{
				Log.Message(patched.ToString() + "." + methodName + " not found");
			}
			harmony.Patch(oMethod, prefix: new HarmonyMethod(pMethod));
		}
		public static void Postfix(Type original, Type patched, String methodName)
		{
			MethodInfo oMethod = original.GetMethod(methodName, bf);
			MethodInfo pMethod = patched.GetMethod(methodName);
			if (null == oMethod)
			{
				Log.Message(original.ToString() + "." + methodName + " not found");
			}
			if (null == pMethod)
			{
				Log.Message(patched.ToString() + "." + methodName + " not found");
			}
			harmony.Patch(oMethod, postfix: new HarmonyMethod(pMethod));
		}

		public static void Transpile(Type original, Type patched, String methodName)
		{
			MethodInfo oMethod = original.GetMethod(methodName, bf);
			MethodInfo pMethod = patched.GetMethod(methodName);
			harmony.Patch(oMethod, transpiler: new HarmonyMethod(pMethod));
		}

		public static void Transpile(Type original, Type patched, String methodName, Type[] orig_type)
		{
			MethodInfo oMethod = original.GetMethod(methodName, bf, null, orig_type, null);
			Type[] patch_type = new Type[orig_type.Length];
			Array.Copy(orig_type, patch_type, orig_type.Length);

			if (!oMethod.ReturnType.Name.Equals("Void"))
			{
				Type[] temp_type = patch_type;
				patch_type = new Type[temp_type.Length + 1];
				patch_type[0] = oMethod.ReturnType.MakeByRefType();
				Array.Copy(temp_type, 0, patch_type, 1, temp_type.Length);
			}
			if (!oMethod.IsStatic)
			{
				Type[] temp_type = patch_type;
				patch_type = new Type[temp_type.Length + 1];
				patch_type[0] = original;
				Array.Copy(temp_type, 0, patch_type, 1, temp_type.Length);
			}

			MethodInfo pMethod = patched.GetMethod(methodName);
			if (null == oMethod)
			{
				Log.Message(original.ToString() + "." + methodName + "(" + string.Join(",", orig_type.Select(x => x.ToString()).ToArray()) + ") not found");
			}
			if (null == pMethod)
			{
				Log.Message(patched.ToString() + "." + methodName + "(" + string.Join(",", patch_type.Select(x => x.ToString()).ToArray()) + ") not found");
			}
			harmony.Patch(oMethod, transpiler: new HarmonyMethod(pMethod));
		}

		public static void Transpile(Type original, Type patched, String methodName, string[] harmonyAfter)
		{
			MethodInfo oMethod = original.GetMethod(methodName, bf);
			MethodInfo pMethod = patched.GetMethod(methodName);
            HarmonyMethod transpilerMethod = new HarmonyMethod(pMethod)
            {
                after = harmonyAfter
            };
            harmony.Patch(oMethod, transpiler: transpilerMethod);
		}
	}

}

