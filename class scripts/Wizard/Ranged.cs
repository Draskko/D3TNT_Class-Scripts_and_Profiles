/*
	Wizard Class Script by ASWeiler
    Last Edited: 11/5/2012
    Thanks To: D3TNT for AOE Avoid and original Core.cs

    This file is part of Astronaut.
    All rights reserved to Astronaut team
    Copyright (C) 2011 Astronaut Team
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

using Astronaut.Bot;
using Astronaut.Common;
using Astronaut.Scripting;
using Astronaut.Scripts.Common;
using Astronaut.D3;
using Astronaut.Monitors;
using System.Threading;
using Astronaut.Scripts;

namespace Astronaut.Scripts.Wizard
{
    public class Core : GlobalBaseBotState
    {
		// Set RestHP to 40%
        public static int RestHp = 40;
		
		// Start DoEnter! (The main function)
        protected override void DoEnter(D3Player Entity)
        {
			// Set the base of DoEnter based off class
            base.DoEnter(Entity);
            // Init Message
            D3Control.output("Class Script by ASWeiler Loaded");
            // We override the default states with our own
            combatState = new Wizard.CombatState();
        }

		// Set that we are not a Healer
        protected override bool IsHealer()
        {
            return false;
        }
	
		// Start the NeedRest bool to false
        public override bool NeedRest(D3Player player)
        {
            return false;
        }
    }
    public class CombatState : Common.CombatState
    {
        /* CONFIGURATION OPTIONS */
		static int ouputMode = 1;	// 0 = Minimal | 1 = Basic Info | 2 = Full Debug (Shows up in All tab of Running Info's tab)
		static bool FocusPackLeader = true;     // focus target elite pack leader (pre 1.0.4 invuln minion workaround)
		static bool FocusTreasureGoblin = true;	// Focus attacking Treasure Goblin until dead	
        static int  RegularMobScanDistance = 50;// scan radius for regular mobs (maximum 100 or about two screens)
        static int  EliteMobScanDistance = 50;	// scan radius for elite mobs (maximum 100 or about two screens)
		const int attackDistance = 50; // Set how far back we attack from
		// Use On HP % Settings
		static int hpPct_UsePotion = 70; // The HP % to use a potion on
		static int hpPct_DiamondSkinNormal = 90; // The HP % to cast Diamond Skin vs Normal mobs
		static int hpPct_DiamondSkinElite = 90; // The HP % to cast Diamond Skin vs Elite mobs
		// Health Globe Settings
		static int hpPct_HealthGlobe = 60; // The HP % to search for a health globe.
		static int PowerHungry_HealthGlobe = 40; // If you use Power Hungry passive skill, set the Arcane Power level you want to search for a health globe.
        /* END CONFIGURATION OPTIONS */
		
		// Best not to change this, this controls how fast we cast a spell
		const int skillInterval = 50; // Set how long we should wait after casting a spell
		
		// Move back timer in seconds
        static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000);
		//
		static CWSpellTimer checkHPTimer = new CWSpellTimer(3 * 1000);
		// Loot Check timer
		static CWSpellTimer checkLootTimer = new CWSpellTimer(1 * 1000, false);
		// Buff Check timer
		static CWSpellTimer checkBuffTimer = new CWSpellTimer(1 * 1000, false);		
        // Checking for safe spot timer (default 1 sec)
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1 * 1000); // checkSafeSpotTimers
        // Escape AOE Thread
		static Thread checkforAOE;
        static Vector3D safeSpot = null;
        //
		static CWSpellTimer teleportTimer = new CWSpellTimer(1 * 1000);
        static CWSpellTimer meteorTimer = new CWSpellTimer(3 * 1000);
        static CWSpellTimer blizzardTimer = new CWSpellTimer(6 * 1000);
        static CWSpellTimer familiarTimer = new CWSpellTimer(5 * 60 * 1000);
        static CWSpellTimer MagicWeaponTimer = new CWSpellTimer(5 * 60 * 1000);
		static bool isPowerHungry = false;
		//
        static bool saveForBlizz = false;
        static bool saveForMeteor = false;
		//
        const int HydraNumber = 1;
		//
        #region ability
        static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();
        #endregion
		//
        public CombatState()
        {
            checkforAOEIsAlive();
            checkforAOE = new Thread(new ThreadStart(keepSpell));
            checkforAOE.IsBackground = true;
            checkforAOE.Start();
        }
		//
        public static void checkforAOEIsAlive()
        {
            try
            {
                if (checkforAOE != null && checkforAOE.IsAlive)
                {
                    checkforAOE.Abort();
                }
            }
            catch
            {
            }
        }

        public static void keepSpell()
        {
            bool isWorking = true;
            try
            {
                while (isWorking)
                {
                    if (!D3Control.IsInGame())
                        return;
					// Wave of Force
					var mobs40 = D3Control.TargetManager.GetAroundEnemy(40).Count;
					if (mobs40 > 1)
					{
						if (D3Control.canCast("Wave of Force"))
						{
							CastWizardSpell("Wave of Force", D3Control.Player.Location);
						}
					}
					// AOE Spells for mob withing 12 radius
					var mobs20 = D3Control.TargetManager.GetAroundEnemy(20).Count;
					if (mobs20 > 0)
					{
						// Frost Nova
						if (D3Control.canCast("Frost Nova"))
						{
							CastWizardSpell("Frost Nova", D3Control.Player.Location);
						}
						// Slow Time
						if (D3Control.canCast("Slow Time"))
						{
							CastWizardSpell("Slow Time", D3Control.Player.Location);
						}
					}
					// Explosive Blast
					if (D3Control.canCast("Explosive Blast") && D3Control.Player.ArcanePower >= 40 && D3Control.Player.isInCombat) //explosive blast on 6sec cd to open doors
					{
						CastWizardSpell("Explosive Blast", D3Control.Player.Location);
					}
					// Check for AOE to avoid
                    if (checkSafeSpotTimer.IsReady)
                    {
                        checkSafeSpotTimer.Reset();
                        var dTargetInfo = D3Control.getDangourousTargetInfo();
                        safeSpot = D3Control.getSafePoint(dTargetInfo);
                    }
                    Thread.Sleep(200);
                }
            }
            catch
            {
            }
        }
        
		// Start with telling bot we have not started the script yet.
        public static bool inited = false;
		
		// Main function to start the Class Script Wizard
        protected override void DoEnter(D3Player Entity)
        {
			// If we have not started yet, start!
            if (!inited)
            {
				// Set that we have started
                inited = true;
				
				// Set teh spe
                setMana();
            }
        }
		
		// Set the spells we have to cast as a Wizard
        internal static void setMana()
        {
            if (abilityInfoDic.Count == 0)
            {
                abilityInfoDic.Add("Arcane Orb", 35);
                abilityInfoDic.Add("Wave of Force", 25);
				abilityInfoDic.Add("Frost Nova", 25);
                abilityInfoDic.Add("Arcane Torrent", 20);
                abilityInfoDic.Add("Energy Twister", 35);
                abilityInfoDic.Add("Ray of Frost", 20);
                abilityInfoDic.Add("Disintegrate", 20);
                abilityInfoDic.Add("Teleport", 15);
                abilityInfoDic.Add("Hydra", 15);
                abilityInfoDic.Add("Meteor", 60);
                abilityInfoDic.Add("Blizzard", 45);
                abilityInfoDic.Add("Explosive Blast", 20);
                abilityInfoDic.Add("Ice Armor", 25);
                abilityInfoDic.Add("Storm Armor", 25);
                abilityInfoDic.Add("Magic Weapon", 25);
                abilityInfoDic.Add("Familiar", 20);
                abilityInfoDic.Add("Energy Armor", 25);
                abilityInfoDic.Add("Archon", 25);			// STILL BROKEN (PM D3TNT TO GET IT FIXED)
                abilityInfoDic.Add("Mirror Image", 0);
            }
        }
		
		// Function to try to cast a Wizard Spell using SpellID and location to cast spell
        public static bool CastWizardSpell(string SpellID, Vector3D loc)
        {
            if (SpellID == "Spectral Blade")
            {
                return D3Control.CastDirectionSpell(SpellID, loc);
            } else if (SpellID == "Teleport" && hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastDirectionSpell(SpellID, loc);
            }
            else if (hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastLocationSpell(SpellID, loc, true);
            }
            return false;
        }
        public static bool CastWizardSpell(string SpellID, Vector3D loc, bool t)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastLocationSpell(SpellID, loc, true);
            }
            return false;
        }
		
        public static bool CastWizardTargetSpell(string SpellName, D3Object target)
        {
            if (hasEnoughResourceForSpell(SpellName))
            {
                return D3Control.CastTargetSpell(SpellName, target);
            }
            return false;
        }
		
		// Function to check if we got the resources to cast the spell based the SpellID given
        public static bool hasEnoughResourceForSpell(string SpellID)
        {
			// If we got a spell on our tab that has that SpellID then continue
            if (abilityInfoDic.ContainsKey(SpellID))
            {
				// If the player does not have the arcane power needed to cast the spell return false
                if (D3Control.Player.ArcanePower < abilityInfoDic[SpellID])
				{
                    return false;
				}
                // If the player does have the arcane power to cast the spell then return true
				else
				{
                    return true;
				}
            }
            else
			{
                return true;
			}
        }
		//
        internal static bool isWithinAttackDistance(D3Unit target)
        {
			// First check if we are close enough to an special target. If we are return true;
			// 	   	  170324 & 139454 is Act 1 Tree Normal
			//				   218947 is Elite Worm in Act 2
			//				   221291 is Act 2 Jumpers from wall in Sundered Canyon
			//				   3384 is the birds in Act 2
			//				   3037 is the summoning thing in Act 2
			//				   3349 IS BELIAL
            //				   193077 is Heart of Sin
            //                 230725 are the beasts on the side of the Rakkis Crossing bridge
            //                 96192  is Siegebreaker
            //                 89690  is Azmodan
			//				   95250  is Queen before Core of Arreat
			//				   137139 is Spiders to Queen
			//				   203048 is Worm in Act 3 - Arreat Creater
            if (((int)target.DistanceFromPlayer <= attackDistance) || 
                ((target.ID == 193077) && ((int)target.DistanceFromPlayer <= 30)) || 
                ((target.ID == 230725) && ((int)target.DistanceFromPlayer <= 10)) ||
                ((target.ID == 96192)  && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 220313) && ((int)target.DistanceFromPlayer <= 40)) ||
				((target.ID == 144315) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 5581) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 495) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 5188) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 5191) && ((int)target.DistanceFromPlayer <= 40)) ||
				((target.ID == 218947) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 170324) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 139454) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 221291) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 3384) && ((int)target.DistanceFromPlayer <= 10)) ||
				((target.ID == 3037) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 95250) && ((int)target.DistanceFromPlayer <= 20)) ||
				((target.ID == 137139) && ((int)target.DistanceFromPlayer <= 20)) ||
                ((target.ID == 89690)  && ((int)target.DistanceFromPlayer <= 45))				
				)
                    return true;

            return false;
        }

        bool pickTarget()
        {
            int enemyCount = 0;
            bool found = false;
            float closestEliteDistance = 50, closestMobDistance = 50;
            D3Unit closestElite = null, closestMob = null, u;

            D3Control.TargetManager.ClearTarget();

            // 65 is about one full screen length
            var mobs = D3Control.TargetManager.GetAroundEnemy(50);
            foreach (D3Unit mob in mobs) 
            {
				if (mob.Untargetable)
				{
					D3Control.output("The target is Untargetable, skipping.");
					D3Control.TargetManager.ignoreTarget(mob);
				}
                enemyCount++;
                if (!mob.IsDead && !D3Control.pureLOS(mob.Location) && !found) 
                {
					if (!mob.IsElite && (mob.DistanceFromPlayer < closestMobDistance)) 
                    {
                        closestMobDistance = mob.DistanceFromPlayer;
                        closestMob = mob;
                    }
					//
                    if (mob.IsElite && (mob.DistanceFromPlayer < closestEliteDistance)) 
                    {
                        closestEliteDistance = mob.DistanceFromPlayer;
                        closestElite = mob;
                    }
					//
					if (mob.ID == 5985 && FocusTreasureGoblin)
					{
                        D3Control.output("Found Treasure Goblin (ID: " + mob.ID + " Dist: " + (int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;					
					}
                    if ((mob.MLevel == 2) && FocusPackLeader)
                    {
                        D3Control.output("Found Pack Leader ID: "+mob.ID+" Dist: "+(int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;
                    }
                }
            }
			// Make sure there is one set
			if (closestMob == null)
			{
				return false;
			}
            // kill all trash mobs within RegularMobScanDistance 
            if ((closestMobDistance <= RegularMobScanDistance) && (closestMob != null)) {
                D3Control.TargetManager.SetAttackTarget(closestMob);
                found = true;
            }
			// kill all elites within EliteMobScanDistance
            if ((closestEliteDistance <= EliteMobScanDistance) && (closestElite != null)) {
                D3Control.TargetManager.SetAttackTarget(closestElite);
                found = true;
            }		
			// If we found a mob to attack, return true
            if (found)
            {
                return true;
            }
			// If we did not find a mob to attack, return false
            else
            {
				return false;
            }
        }
		
        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        protected override void DoExecute(D3Player Entity)
        {
            // return after 10 seconds regardless
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);
			//
            D3Unit originalTarget, u;
            originalTarget = D3Control.curTarget;
			//
			if (originalTarget.ID == 113983)
				return;
            if (!pickTarget())
            {
                // if we don't pick a target we must use the default provided else we will
                // get into a deadlock
                u = originalTarget;
                if (ouputMode > 1)
					D3Control.output("DoEx Orig ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                D3Control.TargetManager.SetAttackTarget(originalTarget);
            }
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(50);
				// If the combat timer expires, break out of DoExecute
                if (combatThresholdTimer.IsReady)
				{
					if (ouputMode > 1)
						D3Control.output("Combat Timer Expired!");
				   break;
				}
				// If we are not in game, or are dead, or the combat timer reset, break out of DoExecute
                if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                    break;
				// Update dungeon info
                D3Control.updateDungoneInfo();
				// Check if we need to use a potion
				if (D3Control.Player.HpPct < hpPct_UsePotion)
				{
					//D3Control.output("HP % is under "+hpPct_UsePotion+". Taking a potion now");
					D3Control.usePotion();
				}
				// If we are low in health look for health globe and move to it if there is one
				// If we have the Power Hungry passive skill and low in arcane, move to a health globe if it exists
				if (D3Control.Player.HpPct <= hpPct_HealthGlobe || (isPowerHungry && D3Control.Player.ArcanePower < PowerHungry_HealthGlobe))
				{
					// Get closest health globe as a variable
					D3Object healthGlobe = D3Control.ClosestHealthGlobe;
					// If we find a health globe, then get within 2.5f from it to pick it up.
					if (healthGlobe != null)
					{
						if (isPowerHungry && D3Control.Player.ArcanePower < 40)
						{
							D3Control.output("The Wizard is Power Hungry!");
						}
						else if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
						{
							D3Control.output("Running to Health Globe");
						}
						// Teleport
						if (D3Control.canCast("Teleport"))
						{
							CastWizardTargetSpell("Teleport", healthGlobe);
						}
						else
						{
							D3Control.MoveTo(healthGlobe, 2.5f);
						}
					}
				}
				// Check For Loot
                if (checkLootTimer.IsReady)
                {
                    D3Control.checkLoots();
                    checkLootTimer.Reset();
                }
				// Check if we need to re-cast buffs
				if (checkBuffTimer.IsReady)
				{
					checkBuff(Entity);
					checkBuffTimer.Reset();
				}
				escapeAOE();
				// Do Pulling if we got a target to kill!
                if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                {
                    D3Control.TargetManager.handleSpecialFight();
                    attackTarget(D3Control.Player);
                    continue;
                }
                else
                {
                    if (!pickTarget())
                    {
                        if (ouputMode > 1)
							D3Control.output("No target found, returning from DoExecute.");
                        break;
                    }
                }
            }
		}
		// This is where we try and cast one of the Signature spells then return out to try other spells or get new target
        bool castPrimary(Vector3D loc)
        {
			// Shock Pulse
            if (D3Control.canCast("Shock Pulse"))
			{
				CastWizardSpell("Shock Pulse", loc);
				return true;
            }
			// Spectral Blade
            if (D3Control.canCast("Spectral Blade"))
            {
				CastWizardSpell("Spectral Blade", loc);
				return true;
            }
            // Electrocute
			if (D3Control.canCast("Electrocute"))
			{
				CastWizardSpell("Electrocute", loc);
				return true;
            }
			// Magic Missle
            if (D3Control.canCast("Magic Missile"))
			{
				CastWizardSpell("Magic Missile", loc);
				return true;
            }
            //
            return false;
        }
		// Cast Secondary 
        bool castSecondary(D3Object target)
        {
            // Make sure the target is still valid
            if (!D3Control.isObjectValid(target))
			{
                return false;
			}
            // Get target location and your location for getting sidespot for some spells.
            Vector3D location = target.Location;
			// Arcane Orb
			if (D3Control.canCast("Arcane Orb"))
            {
				CastWizardSpell("Arcane Orb", location);
				return true;
            }
			// Ray of Frost
            if (D3Control.canCast("Ray of Frost"))
			{
				CastWizardSpell("Ray of Frost", location);
				return true;
            }
			// Arcane Torrent
			if (D3Control.canCast("Arcane Torrent"))
            {
				CastWizardSpell("Arcane Torrent", location);
				return true;
            }
			// Disintegrate
			if (D3Control.canCast("Disintegrate"))
            {
				CastWizardSpell("Disintegrate", location);
				return true;
            }
			// Return False
            return false;
        }	
		// This is where we check for buffs to keep up.
        public static void checkBuff(D3Player entity)
        {
            //
            if (!D3Control.HasBuff("Ice Armor") && D3Control.canCast("Ice Armor"))
            {
                if (CastWizardSpell("Ice Armor", D3Control.Player.Location))
                    Thread.Sleep(skillInterval);
            }
			//
            if (!D3Control.HasBuff("Storm Armor") && D3Control.canCast("Storm Armor"))
            {
                CastWizardSpell("Storm Armor", D3Control.Player.Location);
            }
			//
            if (!D3Control.HasBuff("Energy Armor") && D3Control.canCast("Energy Armor"))
            {
                CastWizardSpell("Energy Armor", D3Control.Player.Location);
            }
			//
            if (MagicWeaponTimer.IsReady && CastWizardSpell("Magic Weapon", D3Control.Player.Location))
            {
                MagicWeaponTimer.Reset();
            }
			//
            if (D3Control.curTarget.IsElite && D3Control.Player.HpPct < hpPct_DiamondSkinElite && D3Control.canCast("Diamond Skin"))
            {
				D3Control.CastLocationSpell("Diamond Skin", D3Control.Player.Location, true);
            }
			//
            if (D3Control.Player.HpPct < hpPct_DiamondSkinNormal && D3Control.canCast("Diamond Skin"))
            {
				D3Control.CastLocationSpell("Diamond Skin", D3Control.Player.Location, true);
            }
			//
            if (D3Control.canCast("Familiar") && familiarTimer.IsReady)
            {
                D3Control.CastLocationSpell("Familiar", D3Control.Player.Location, true);
                familiarTimer.Reset();
            }
        }
		// Start the attack!
        void attackTarget(D3Player entity)
        {
			Thread.Sleep(100);
			//
            if (!GlobalBaseBotState.checkBeforePull(entity))
				return;
			// If we are low in health look for health globe and move to it if there is one
			// If we have the Power Hungry passive skill and low in arcane, move to a health globe if it exists
			if (D3Control.Player.HpPct <= hpPct_HealthGlobe || (isPowerHungry && D3Control.Player.ArcanePower < PowerHungry_HealthGlobe))
			{
				// Get closest health globe as a variable
				D3Object healthGlobe = D3Control.ClosestHealthGlobe;
				// If we find a health globe, then get within 2.5f from it to pick it up.
				if (healthGlobe != null)
				{
					if (isPowerHungry && D3Control.Player.ArcanePower < 40)
					{
						D3Control.output("The Wizard is Power Hungry!");
					}
					else if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
					{
						D3Control.output("Running to Health Globe");
					}
					// Teleport
					if (D3Control.canCast("Teleport"))
					{
						CastWizardTargetSpell("Teleport", healthGlobe);
					}
					else
					{
						D3Control.MoveTo(healthGlobe, 2.5f);
					}
				}
			}
			// Check if we need to re-cast buffs
			if (checkBuffTimer.IsReady)
			{
				checkBuff(entity);
				checkBuffTimer.Reset();
			}
			// Check For Loot
            if (checkLootTimer.IsReady)
            {
                D3Control.checkLoots();
                checkLootTimer.Reset();
            }
			// Check if we need to use a potion
			if (D3Control.Player.HpPct < hpPct_UsePotion)
			{
				//D3Control.output("HP % is under "+hpPct_UsePotion+". Taking a potion now");
				D3Control.usePotion();
			}
			escapeAOE();
			// Set the target variable as the current target set in the TargetManager
            D3Unit target = D3Control.curTarget;				
			// If we detect a door, deal with it.
            if (D3Control.getDoorInWay(target) != null)
            {
				// Move closer to the door
                D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(target, 2.5f);      // a thread doing the move
                return;
            }			
			// If we are trying to move forward, but we arn't, attack in front of ourselves to clear a path
			if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
			{
				D3Control.output("Trying to move, but we arn't fire spells to clear things in the way");
				Vector3D loc = D3Control.getSideSpot(target.Location, 0, 20);
				// Explosive Blast
				if (D3Control.canCast("Explosive Blast"))
					CastWizardSpell("Explosive Blast", D3Control.Player.Location);
				// Energy Twister
				if (D3Control.canCast("Energy Twister"))
					CastWizardSpell("Energy Twister", loc);
				// Primary Spells
				castPrimary(loc);
				return;
			}
			if (D3Control.isObjectValid(target) && !D3Control.isMovingWorking() && !D3Control.Player.isMovingForward && !target.IsDead && !D3Control.LOS(target.Location))
			{
				D3Control.output("Attacking "+target.ID+" Dist: "+target.DistanceFromPlayer+" HP: "+target.Hp);
				// Teleport
				if (!D3Control.LOS(target.Location) && !D3Control.pureLOS(target.Location) && target.DistanceFromPlayer > 10 && target.DistanceFromPlayer <= 40 && teleportTimer.IsReady && D3Control.canCast("Teleport"))
				{
					CastWizardSpell("Teleport", target.Location);
					teleportTimer.Reset();
				}
				handleAOE(target.Location);
				if (D3Control.isObjectValid(target) && D3Control.canCast("Hydra"))
				// Hydra
				{
					int curCount = D3Control.getWizHydraNumber();
					for (int j = 0; j < HydraNumber - curCount; j++)
					{
						if (CastWizardSpell("Hydra", target.Location))
						{
							Thread.Sleep(50);
						}
					}
				}
				// Try an secondary spell
				castSecondary(target);
				// Try a primary spell
				castPrimary(target.Location);
				// Now get a new target based off the closest target found
				D3Control.TargetManager.ClearTarget();
                D3Object ClosestEnemy = D3Control.ClosestEnemy;
                D3Control.TargetManager.SetAttackTarget((D3Unit)ClosestEnemy);
			}
            // nothing in range or no LOS to target so lets move closer
            if ((!isWithinAttackDistance(target) || D3Control.LOS(target.Location)) && (!D3Control.isMovingWorking() && !D3Control.Player.isMovingForward && !target.IsDead))
            {
				if (D3Control.LOS(target.Location))
				{
					D3Control.output("Not within Line of Sight, moving closer");
				}
				else
				{
					D3Control.output("Not within Attack Distance, moving closer");
				}
				float d = D3Control.curTarget.DistanceFromPlayer;
                D3Control.MoveTo(target, d - 10);
                Thread.Sleep(50);
            }
        }
		
		bool handleAOE(Vector3D loc)
		{
            // Make sure the target is still valid
            if (!D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead)
			{
                return false;
			}
			// Wave of Force
			var mobs40 = D3Control.TargetManager.GetAroundEnemy(40).Count;
			if (mobs40 > 0)
			{
				if (D3Control.canCast("Wave of Force"))
				{
					CastWizardSpell("Wave of Force", D3Control.Player.Location);
				}
			}
			// AOE Spells for mobs within 15 radius
			var mobs20 = D3Control.TargetManager.GetAroundEnemy(20).Count;
			if (mobs20 > 0)
			{
				// Frost Nova
				if (D3Control.canCast("Frost Nova"))
				{
					CastWizardSpell("Frost Nova", D3Control.Player.Location);
				}
				// Slow Time
				if (D3Control.canCast("Slow Time"))
				{
					CastWizardSpell("Slow Time", D3Control.Player.Location);
				}
				// Explosive Blast
				if (D3Control.canCast("Explosive Blast") && D3Control.Player.ArcanePower >= 40)
				{
					CastWizardSpell("Explosive Blast", D3Control.Player.Location);
				}
			}
			// Energy Twister
			if (D3Control.curTarget.DistanceFromPlayer <= 20 && D3Control.canCast("Energy Twister") && D3Control.Player.ArcanePower >= 60)
			{
				CastWizardSpell("Energy Twister", loc);
			}
			// Blizzard
			if (blizzardTimer.IsReady && D3Control.canCast("Blizzard") && D3Control.Player.ArcanePower >= 40)
			{
				CastWizardSpell("Blizzard", loc);
				blizzardTimer.Reset();
			}
			// Meteor
			if (meteorTimer.IsReady && D3Control.canCast("Meteor") && D3Control.Player.ArcanePower >= 40)
			{
				CastWizardSpell("Meteor", loc);
				meteorTimer.Reset();
			}
			return true;
		}
        
        bool escapeAOE()
        {
            // if you dont want to avoid aoe, uncomment the return statement
            // return false;
            if (safeSpot != null && moveBackTimer.IsReady)
            {
				// Set tempSpot variable from safeSpot variable
                var tempSpot = safeSpot;
				// Reset safeSpot
                safeSpot = null;
                runtosafespot(tempSpot);
				// Done!
                D3Control.output("Escaped AOE");
                return true;
            }
            return false;
        }
        
        void runtosafespot(Vector3D location)
        {
            // if you dont want to avoid aoe, uncomment the return statement
            if (moveBackTimer.IsReady)
            {
				// Teleport
				if (D3Control.canCast("Teleport"))
				{
					CastWizardSpell("Teleport", location);
				}
				else
				{
					D3Control.ClickMoveTo(location);
					while (D3Control.Player.isMovingForward)
					{
						Thread.Sleep(50);
					}
				}
				moveBackTimer.Reset();
            }
        }
        
        void moveback(int length)
        {
            if (moveBackTimer.IsReady)
            {
                if (D3Control.curTarget.DistanceFromPlayer < attackDistance)
                {
                    moveBackTimer.Reset();
					// Teleport
					if (D3Control.canCast("Teleport"))
					{
						CastWizardSpell("Teleport", D3Control.getSideSpot(D3Control.Player.Location, 180, length));
						Thread.Sleep(skillInterval);
					}
					else
					{
						D3Control.ClickMoveTo(D3Control.getSideSpot(D3Control.Player.Location, 180, length));
						while (D3Control.Player.isMovingForward)
						{
							Thread.Sleep(50);
						}
					}
                }
            }
        }
		
        protected override void DoExit(D3Player entity)
        {
            //on exit, if there is a previous state, go back to it
            if (PreviousState != null)
            {
                CallChangeStateEvent(entity, PreviousState, false, false);
            }
        }

        protected override void DoFinish(D3Player entity)
        {

        }
    }/**/
    /**/
}