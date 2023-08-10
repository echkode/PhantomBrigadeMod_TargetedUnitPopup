using System.Collections.Generic;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Data;
using PhantomBrigade.Input.Components;
using PBCIViewCombatTimeline = CIViewCombatTimeline;

using UnityEngine;

namespace EchKode.PBMods.TargetedUnitPopup
{
	static class CIViewCombatTimeline
	{
		private static Dictionary<int, CIHelperTimelineAction> helpersActionsPlanned;
		private static int dragActionID = IDUtility.invalidID;

		internal static void Initialize()
		{
			helpersActionsPlanned = Traverse.Create(PBCIViewCombatTimeline.ins).Field<Dictionary<int, CIHelperTimelineAction>>(nameof(helpersActionsPlanned)).Value;
		}

		internal static void ConfigureActionPlanned(CIHelperTimelineAction helper, int actionID)
		{
			var (ok, action) = IsEquipmentAction(actionID);
			if (!ok)
			{
				return;
			}

			if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.ActionHook))
			{
				var activePart = IDUtility.GetEquipmentEntity(action.activeEquipmentPart.equipmentID);
				Debug.LogFormat(
					"Mod {0} ({1}) Installing hover callbacks on action | action ID: {2} | owner combat ID: {3} | active part ID: {4}",
					ModLink.modIndex,
					ModLink.modID,
					actionID,
					action.hasActionOwner ? action.actionOwner.combatID : -99,
					activePart.id.id);
			}

			UIHelper.ReplaceCallbackObject(ref helper.button.callbackOnHoverStart, OnActionHoverStart, helper.button.callbackOnHoverStart);
			UIHelper.ReplaceCallbackObject(ref helper.button.callbackOnHoverEnd, OnActionHoverEnd, helper.button.callbackOnHoverEnd);
		}

		internal static void OnActionDrag(object callbackAsObject)
		{
			if (ModLink.Settings.showOnDrag)
			{
				// On drag start, the cursor is transitioning from a hover status so the popup is already
				// being displayed.
				return;
			}

			if (!CombatUIUtility.IsCombatUISafe())
			{
				return;
			}
			if (Contexts.sharedInstance.input.combatUIMode.e != CombatUIModes.Unit_Selection)
			{
				return;
			}
			if (!(callbackAsObject is UICallback uiCallback))
			{
				return;
			}

			int argumentInt = uiCallback.argumentInt;
			if (!helpersActionsPlanned.ContainsKey(argumentInt))
			{
				return;
			}
			if (dragActionID == argumentInt)
			{
				return;
			}
			var (ok, action) = IsEquipmentAction(argumentInt);
			if (!ok)
			{
				return;
			}

			HideTargetedUnit();
			dragActionID = action.id.id;
		}

		internal static void OnActionDragEnd(object callbackAsObject)
		{
			if (ModLink.Settings.showOnDrag)
			{
				// On drag end, the cursor reverts to a hover status. We don't need to do anything
				// since we're already showing the popup.
				return;
			}

			if (!(callbackAsObject is UICallback uiCallback))
			{
				return;
			}
			int argumentInt = uiCallback.argumentInt;
			if (!helpersActionsPlanned.ContainsKey(argumentInt))
			{
				return;
			}
			if (dragActionID != argumentInt)
			{
				return;
			}
			var (ok, action) = IsEquipmentAction(argumentInt);
			if (!ok)
			{
				return;
			}

			ShowTargetedUnit(action);
			dragActionID = IDUtility.invalidID;
		}

		static void OnActionHoverStart(object arg)
		{
			if (!(arg is UICallback priorCallback))
			{
				return;
			}
			priorCallback.Invoke();

			var isSimulating = Contexts.sharedInstance.combat.Simulating;
			if (isSimulating)
			{
				return;
			}
			if (Contexts.sharedInstance.input.combatUIMode.e != CombatUIModes.Unit_Selection)
			{
				return;
			}

			var action = IDUtility.GetActionEntity(priorCallback.argumentInt);
			if (action == null)
			{
				return;
			}

			if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.OnHover))
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Hover start on action | action ID: {2}",
					ModLink.modIndex,
					ModLink.modID,
					action.id.id);
			}

			ShowTargetedUnit(action);
		}

		static void OnActionHoverEnd(object arg)
		{
			if (!(arg is UICallback priorCallback))
			{
				return;
			}
			priorCallback.Invoke();

			var isSimulating = Contexts.sharedInstance.combat.Simulating;
			if (isSimulating)
			{
				return;
			}
			if (Contexts.sharedInstance.input.combatUIMode.e != CombatUIModes.Unit_Selection)
			{
				return;
			}

			if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.OnHover))
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Hover end on action",
					ModLink.modIndex,
					ModLink.modID);
			}

			HideTargetedUnit();
		}

		static void ShowTargetedUnit(ActionEntity action)
		{
			if (!action.hasTargetedEntity)
			{
				return;
			}

			var targetedUnit = IDUtility.GetCombatEntity(action.targetedEntity.combatID);
			if (targetedUnit == null)
			{
				return;
			}
			if (!targetedUnit.hasPosition)
			{
				return;
			}

			CombatUITargeting.OnTimelineUI(targetedUnit.id.id);
		}

		static void HideTargetedUnit()
		{
			CombatUITargeting.OnTimelineUI(IDUtility.invalidID);
		}

		static (bool, ActionEntity) IsEquipmentAction(int actionID)
		{
			var action = IDUtility.GetActionEntity(actionID);
			if (action == null)
			{
				return (false, null);
			}
			if (action.isMovementExtrapolated)
			{
				return (false, null);
			}
			if (!action.hasStartTime)
			{
				return (false, null);
			}
			if (!action.hasDuration)
			{
				return (false, null);
			}
			if (!action.hasActiveEquipmentPart)
			{
				return (false, null);
			}

			var activePart = IDUtility.GetEquipmentEntity(action.activeEquipmentPart.equipmentID);
			if (activePart == null)
			{
				return (false, null);
			}
			if (!activePart.hasPrimaryActivationSubsystem)
			{
				return (false, null);
			}

			if (!ModLink.Settings.showPopupsForShields && IsShieldAction(action))
			{
				if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.ActionHook))
				{
					Debug.LogFormat(
						"Mod {0} ({1}) Equipment action check | popup disabled for shields | action ID: {2}",
						ModLink.modIndex,
						ModLink.modID,
						action.id.id);
				}
				return (false, null);
			}

			return (true, action);
		}

		static bool IsShieldAction(ActionEntity action)
		{
			var (dataOK, actionData) = GetActionData(action);
			if (!dataOK)
			{
				if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.ActionHook))
				{
					Debug.LogFormat(
						"Mod {0} ({1}) Is shield action | action data not OK | action ID: {2}",
						ModLink.modIndex,
						ModLink.modID,
						action.id.id);
				}
				return false;
			}
			var (partOK, _, part) = GetPartInUnit(actionData);
			if (!partOK)
			{
				if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.ActionHook))
				{
					Debug.LogFormat(
						"Mod {0} ({1}) Is shield action | part not found on unit | action ID: {2}",
						ModLink.modIndex,
						ModLink.modID,
						action.id.id);
				}
				return false;
			}

			if (ModLink.Settings.IsLoggingEnabled(ModLink.ModSettings.LoggingFlag.ActionHook))
			{
				Debug.LogFormat(
					"Mod {0} ({1}) Is shield action | action ID: {2} | tags: {3}",
					ModLink.modIndex,
					ModLink.modID,
					action.id.id,
					string.Join(",", part.tagCache.tags));
			}

			return part.tagCache.tags.Contains("type_defensive");
		}

		static (bool, DataContainerAction) GetActionData(ActionEntity action)
		{
			if (!action.hasDataLinkAction)
			{
				return (false, null);
			}

			var actionData = action.dataLinkAction.data;
			if (actionData == null)
			{
				return (false, null);
			}
			if (actionData.dataEquipment == null)
			{
				return (false, null);
			}
			if (!actionData.dataEquipment.partUsed)
			{
				return (false, null);
			}
			if (actionData.dataEquipment.partSocket == "core")
			{
				return (false, null);
			}

			return (true, actionData);
		}

		static (bool, CombatEntity, EquipmentEntity) GetPartInUnit(DataContainerAction actionData)
		{
			var selectedCombatUnitID = Contexts.sharedInstance.combat.hasUnitSelected
				? Contexts.sharedInstance.combat.unitSelected.id
				: IDUtility.invalidID;
			var combatEntity = IDUtility.GetCombatEntity(selectedCombatUnitID);
			if (combatEntity == null)
			{
				return (false, null, null);
			}

			var unit = IDUtility.GetLinkedPersistentEntity(combatEntity);
			if (unit == null)
			{
				return (false, null, null);
			}

			var partInUnit = EquipmentUtility.GetPartInUnit(unit, actionData.dataEquipment.partSocket);
			if (partInUnit == null)
			{
				return (false, null, null);
			}

			return (true, combatEntity, partInUnit);
		}
	}
}
