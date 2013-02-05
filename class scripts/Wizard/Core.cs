/*
	Wizard Class Script by ASWeiler
	
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
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Linq;

using Astronaut.Bot;
using Astronaut.D3;
using Astronaut.Common;
using Astronaut.Scripting;
using Astronaut.Scripts;
using Astronaut.Scripts.Common;
using Astronaut.Monitors;
using Astronaut.States;
using Astronaut.Monitors;

namespace Astronaut.Scripts.Wizard
{
    public class Core : GlobalBaseBotState
    {	
        protected override void DoEnter(D3Player Entity)
        {
            base.DoEnter(Entity);
            combatState = new Wizard.CombatState();
			string modified_date = "2/4/2013";
			D3Control.output("ASWeiler's Monk Class Script (last modified "+modified_date+").");
        }
    }
	
    public class CombatState : Common.CombatState
    {	
		/* CONFIGURATION OPTIONS */
		
		/*
			Spell OPTIONS
		*/
		// Teleport OPTIONS
		static int TeleportArcanePowerAmount = 110; // Set this to the amount of Arcane Power you must have before using Teleport
		// Spectral Blade OPTIONS
		static int SpectralBladeDistance = 10; // If the target is within this range, cast Spectral Blade (10 = Default | 20 = Thrown Blade)
		// Shock Pulse OPTIONS
		static int ShockPulseDistance = 25; // If the target is within this range, cast Shock Pulse
		// Electrocute OPTIONS
		static int ElectrocuteDistance = 40; // If the target is within this range, cast Electrocute
		// Frost Nova OPTIONS
        static int FrostNovaRange = 20; // 
		static int EliteFrostNovaRange = 20; // If a Elite is within this range, cast Frost Nova
		// Explosive Blast OPTIONS
		static bool useExplosiveBlastWhileMoving = true; // True = Cast Explosive Blast while walking
		static int ExplosiveBlastDistance = 50;	// If the target is within this range, cast Explosive Blast
		static int ExplosiveBlastArcanePower = 40; // Set this to the amount of Arcane Power you want to have before casting Explosive Blast
		// Slow Time OPTIONS
        static int SlowTimeDistance = 10; // If the target is within this range, cast Slow Time
		static int EliteSlowTimeDistance = 20; // If a Elite is within this range, cast Electrocute
		// Wave of Force OPTIONS
		static int WaveOfForceDistance = 7; // If the target is within this range, cast Wave of Force
		// Blizzard OPTIONS
		static int BlizzardArcanePowerAmount = 40; // Set this to the amount of Arcane Power you must have before you will cast Blizzard
		static CWSpellTimer blizzardTimer = new CWSpellTimer(6 * 1000);
		// Energy Twister OPTIONS
		static int EnergyTwisterArcanePowerAmount = 35; // Set this to the amount of Arcane Power you must have before you will cast Energy Twister
		// Meteor OPTIONS
		static int MeteorArcanePowerAmount = 50; // Set this to the amount of Arcane Power you must have before you will cast Meteor
		// Diamond Skin OPTIONS
		static int DiamondSkinDistance = 30; // Set this to the distance that the current target must be before using Diamond Skin
		
		/* 
			GENERAL OPTIONS
		*/
		// Potion OPTIONS
		static int hpPct_UsePotion = 50; // The HP % to use a potion on
		// Health Globe OPTIONS
		static int hpPct_HealthGlobe = 60; // The HP % to search for a health globe.
		static int PowerHungry_HealthGlobe = 40; // If you use Power Hungry passive skill, set the Arcane Power level you want to search for a health globe.
		// AOE OPTIONS
		static bool AvoidAoE = true; // try to avoid AoE (desecrate and middle of arcane beams)
		// Target OPTIONS
        static int MobScanDistance = 50; // scan radius for regular mobs (maximum 100 or about two screens)
		const int attackDistance = 10; // Set how far back we attack from
		static bool FocusPackLeader = false; // True = focus target elite pack leader until dead
		static bool FocusTreasureGoblin = true;	// True = focus attacking Treasure Goblin until dead
		static bool FocusMobSummoner = true; // True = focus attacking any mob summoners until dead		
		/* 
			ADVANCED SETTINGS 
		*/
		static Thread keepSpellThread;
        static Vector3D safeSpot = null, oldSafeSpot = null;
		const int skillInterval = 150; 				// Set how long we should wait after casting a spell	
		static bool isPowerHungry = false;
		static int outputMode = 1;
        const int HydraNumber = 1;
		public int oldTargetHP = 0;
		public int numofAttacks = 0;
		//
		#region ability
        static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();
		#endregion
        public static bool inited = false;
		/*
			TIMERS
		*/
        static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000);
		static CWSpellTimer lootcheckTimer = new CWSpellTimer(2 * 1000);
		static CWSpellTimer checkBuffTimer = new CWSpellTimer(1 * 1000, false);
		static CWSpellTimer checkInGameTimer = new CWSpellTimer(1 * 1000);
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1 * 1000); // Checking for safe spot timer (default 1 sec)
        static CWSpellTimer familiarTimer = new CWSpellTimer(5 * 60 * 1000);
        static CWSpellTimer MagicWeaponTimer = new CWSpellTimer(5 * 60 * 1000);
		
        public CombatState()
        {
            try 
            {
				//
                if ((keepSpellThread != null) && keepSpellThread.IsAlive)
                {
                    keepSpellThread.Abort();
                    keepSpellThread.Join();
                }
            }
            catch (ThreadStateException e) 
            {
            }
			//
            keepSpellThread = new Thread(new ThreadStart(keepSpell));    
            keepSpellThread.IsBackground = true;
            keepSpellThread.Start();
            while (!keepSpellThread.IsAlive);
        }

        ~CombatState()
        {
        }

        public static void keepSpell()
        {
            CWSpellTimer updateSafeSpotTimer = new CWSpellTimer(1 * 1000);
			
            Random rgen = new Random();
            int seed = rgen.Next();
			if (outputMode > 1)
				D3Control.output("Starting Spell Thread. Seed: " + seed);
            try
            {
                while (true)
                {
					// If we are not in game, skip the rest
					if (!D3Control.IsInGame())          
						return;
                    if (updateSafeSpotTimer.IsReady && D3Control.Player.isInCombat)
                    {
                        updateSafeSpotTimer.Reset();
                        var dTargetInfo = D3Control.getDangourousTargetInfo();
                        safeSpot = D3Control.getSafePoint(dTargetInfo);
                    }
					// Explosive Blast
					if ((D3Control.canCast("Explosive Blast") && D3Control.Player.ArcanePower >= ExplosiveBlastArcanePower && D3Control.Player.isMovingForward) && useExplosiveBlastWhileMoving)
					{
						CastWizardSpell("Explosive Blast", D3Control.Player.Location);
					}
					// Frost Nova
					if (D3Control.canCast("Frost Nova") && D3Control.TargetManager.GetAroundEnemy(FrostNovaDistance).Count > 0)
						CastWizardSpell("Frost Nova", D3Control.Player.Location);
					// Wave of Force
					if (D3Control.canCast("Wave of Force") && D3Control.TargetManager.GetAroundEnemy(WaveOfForceDistance).Count > 0)
						CastWizardSpell("Wave of Force", D3Control.Player.Location);
					// If we are low in HP, use Potion
					if (D3Control.Player.HpPct <= hpPct_UsePotion)
						D3Control.usePotion();
					//
                    Thread.Sleep(300);
                }
            }
            catch
            {
            }
        }
		
		// Main function to start the Class Script Wizard
        protected override void DoEnter(D3Player Entity)
        {
            if (!inited)
            {
                inited = true;
				D3Control.output("ASWeiler's Wizard Loaded");
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
				abilityInfoDic.Add("Frost Nova", 0);
                abilityInfoDic.Add("Arcane Torrent", 16);
                abilityInfoDic.Add("Energy Twister", 35);
                abilityInfoDic.Add("Ray of Frost", 16);
                abilityInfoDic.Add("Disintegrate", 18);
                abilityInfoDic.Add("Teleport", 15);
                abilityInfoDic.Add("Hydra", 15);
                abilityInfoDic.Add("Meteor", 50);
                abilityInfoDic.Add("Blizzard", 40);
                abilityInfoDic.Add("Explosive Blast", 20);
                abilityInfoDic.Add("Ice Armor", 25);
                abilityInfoDic.Add("Storm Armor", 25);
                abilityInfoDic.Add("Magic Weapon", 25);
                abilityInfoDic.Add("Familiar", 20);
                abilityInfoDic.Add("Energy Armor", 25);
                abilityInfoDic.Add("Archon", 25);
				abilityInfoDic.Add("Disintegration Wave", 0);
                abilityInfoDic.Add("Mirror Image", 0);
            }
        }
		
		// Function to try to cast a Wizard Spell using SpellID and location to cast spell
        public static bool CastWizardSpell(string SpellID, Vector3D loc)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
				if (SpellID == "Disintegrate")
				{
					return D3Control.CastDirectionSpell(SpellID, loc);
				}
				else
				{
					return D3Control.CastLocationSpell(SpellID, loc, true);
				}
            }
            return false;
        }
        public static bool NewCastWizardTargetSpell(string SpellName, D3Object target, out bool castSuccessfully)
        {
            castSuccessfully = false;
            if (hasEnoughResourceForSpell(SpellName))
            {
                return D3Control.NewCastTargetSpell(SpellName, target, out castSuccessfully);
                
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
        public static bool hasEnoughResourceForSpell(string SpellID)
        {
            if (abilityInfoDic.ContainsKey(SpellID))
            {
                if (D3Control.Player.ArcanePower < abilityInfoDic[SpellID])
				{
                    return false;
				}
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
		
        internal static bool isWithinAttackDistance(D3Unit target)
        {
            // 10 feet is melee range
            // SPECIAL CASES:  193077 is Heart of Sin
            //                 230725 are the beasts on the side of the Rakkis Crossing bridge
            //                 96192  is Siegebreaker
            //                 89690  is Azmodan
			//				   95250  is Queen before Core of Arreat
			//				   218947 is Elite Worm in Act 2
			bool isCloseEnoughToSpecialTarget = true;
            if (
                ((target.ID == 193077) && ((int)target.DistanceFromPlayer >= 40)) || 
                ((target.ID == 230725) && ((int)target.DistanceFromPlayer >= 13)) ||
                ((target.ID == 96192)  && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 220313) && ((int)target.DistanceFromPlayer >= 40)) ||
				((target.ID == 144315) && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 5581) && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 495) && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 5188) && ((int)target.DistanceFromPlayer >= 10)) ||
				((target.ID == 6572) && ((int)target.DistanceFromPlayer >= 10)) ||
				((target.ID == 139456) && ((int)target.DistanceFromPlayer >= 10)) ||
				((target.ID == 5191) && ((int)target.DistanceFromPlayer >= 40)) ||
				((target.ID == 218947) && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 170324) && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 95250) && ((int)target.DistanceFromPlayer >= 40)) ||
				((target.ID == 139454) && ((int)target.DistanceFromPlayer >= 20)) ||
				((target.ID == 221291) && ((int)target.DistanceFromPlayer >= 10)) ||
				((target.ID == 3384) && ((int)target.DistanceFromPlayer >= 15)) ||
				((target.ID == 3037) && ((int)target.DistanceFromPlayer >= 20)) ||
                ((target.ID == 89690)  && ((int)target.DistanceFromPlayer >= 45))				
				)
			{
				D3Control.output("Special target found, but we were not close enough"+(int)target.DistanceFromPlayer);
				isCloseEnoughToSpecialTarget = false;
				return false;
			}
			else if ((int)target.DistanceFromPlayer > attackDistance && isCloseEnoughToSpecialTarget)
			{
				return false;
			}
			return true;
        }

        bool pickTarget()
        {
            int enemyCount = 0;
            bool found = false;
            float closestMobDistance = MobScanDistance;
            D3Unit closestMob = null, u;

            D3Control.TargetManager.ClearTarget();

            // 65 is about one full screen length
            var mobs = D3Control.TargetManager.GetAroundEnemy(MobScanDistance);
            foreach (D3Unit mob in mobs) 
            {
                // Ignore the Demon Spawned from Maximus weapon
                if (mob.ID == 249320)
                    continue;
				//
				if (mob.Untargetable)
				{
					//D3Control.output("Skipping Untargetable");
					break;
				}
				if (mob.ID == 144315)
				{
					D3Control.output("Skipping Poop Fuckers");
					break;
				}
                enemyCount++;
                if (!mob.IsDead && !D3Control.LOS(mob.Location) && !found) 
                {
                    if(mob.DistanceFromPlayer < closestMobDistance)
                    {
                        closestMobDistance = mob.DistanceFromPlayer;
                        closestMob = mob;
                    }
					//
					if ((mob.ID == 5985 || mob.ID == 5984 || mob.ID == 5985 || mob.ID == 5987 || mob.ID == 5988) && FocusTreasureGoblin)
					{
						if (outputMode > 0)
						D3Control.output("Found Treasure Goblin (ID: " + mob.ID + " Dist: " + (int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;					
					}
					if ((mob.ID == 5388 || mob.ID == 5387 || mob.ID == 4100) && FocusMobSummoner)
					{
						if (outputMode > 0)
						D3Control.output("Found Mob Summoner ID: "+mob.ID+" Dist: "+(int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;
					}
                    if ((mob.MLevel == 2) && FocusPackLeader)
                    {
						if (outputMode > 0)
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
            // kill all trash mobs within RegularMobScanDistance and elites within EliteMobScanDistance
            if ((closestMobDistance <= MobScanDistance) && (closestMob != null)) {
                D3Control.TargetManager.SetAttackTarget(closestMob);
                return true;
            }
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
            if (!pickTarget())
            {
                // if we don't pick a target we must use the default provided else we will
                // get into a deadlock
                u = originalTarget;
                D3Control.TargetManager.SetAttackTarget(originalTarget);
            }
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(10);
                if (combatThresholdTimer.IsReady)
				{
					if (outputMode > 0)
					{
						D3Control.output("Combat Timer Expired!");
					}
					break;
				}
				// Update dungeon info
                D3Control.updateDungoneInfo();
				// If we are not in game, or are dead, or the combat timer reset, break out of DoExecute
                if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                    break;
				// If we are low in HP, use Potion
				if (D3Control.Player.HpPct < hpPct_UsePotion)
				{
					D3Control.usePotion();
				}
				// Check for loot outside combat.
				if (lootcheckTimer.IsReady)
				{
					D3Control.checkLoots();
					lootcheckTimer.Reset();
				}
				// Check if we need to re-cast buffs
				if (checkBuffTimer.IsReady)
				{
					checkBuff(Entity);
					checkBuffTimer.Reset();
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
						if (isPowerHungry && D3Control.Player.ArcanePower < 40 && D3Control.Player.HpPct > 60)
						{
							D3Control.output("The Wizard is Power Hungry!");
						}
						D3Control.MoveTo(healthGlobe, 2.5f);
					}
				}
				//
                if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                {
                    D3Control.TargetManager.handleSpecialFight();
                    attackTarget(D3Control.Player);
                    continue;
                }
				// If we are not in game, died, combat timer expired, or no target was near enough to move to and attack, return from DoExecute.
                else if (!pickTarget())
                {
					// If nothing was found to attack, return from DoExecute
                    if (outputMode > 1)
						D3Control.output("No target found, returning from DoExecute.");
                    break;
                }
            }
        }

		// This is where we try and cast one of the Signature spells then return out to try other spells or get new target
        bool castPrimary(D3Object target)
        {
            // Make sure the target is still valid
            if (!D3Control.isObjectValid(target))
			{
                return false;
			}
			// Teleport
			if ((D3Control.Player.ArcanePower <= TeleportArcanePowerAmount) && target.DistanceFromPlayer <= 60 && target.DistanceFromPlayer > 10 && CastWizardSpell("Teleport", target.Location))
			{
				Thread.Sleep(skillInterval);
				return true;
			}
            if (target.DistanceFromPlayer <= SpectralBladeDistance && CastWizardSpell("Spectral Blade", target.Location))
            {
				Thread.Sleep(skillInterval);
				return true;
            }
			// Shock Pulse
            if (target.DistanceFromPlayer <= ShockPulseDistance && CastWizardSpell("Shock Pulse", target.Location))
            {
				Thread.Sleep(skillInterval);
				return true;
            }
            // Electrocute
			if (target.DistanceFromPlayer <= ElectrocuteDistance && CastWizardSpell("Electrocute", target.Location))
            {
				Thread.Sleep(skillInterval);
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
			bool castSuccessfully = false;
			// Arcane Orb
			if (target.DistanceFromPlayer > 15 && target.DistanceFromPlayer <= 50)
			{
				NewCastWizardTargetSpell("Arcane Orb", target, out castSuccessfully);
				if (castSuccessfully)
				{
					D3Control.output("Arcane Orb");
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Ray of Frost
			if (target.DistanceFromPlayer > 15 && target.DistanceFromPlayer <= 50)
			{
				NewCastWizardTargetSpell("Ray of Frost", target, out castSuccessfully);
				if (castSuccessfully)
				{
					D3Control.output("Ray of Frost");
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Arcane Torrent
			if (target.DistanceFromPlayer > 15 && target.DistanceFromPlayer <= 50)
			{
				NewCastWizardTargetSpell("Arcane Torrent", target, out castSuccessfully);
				if (castSuccessfully)
				{
					D3Control.output("Arcane Torrent");
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Disintegrate
			if (target.DistanceFromPlayer > 15 && target.DistanceFromPlayer <= 50)
			{
				NewCastWizardTargetSpell("Disintegrate", target, out castSuccessfully);
				if (castSuccessfully)
				{
					D3Control.output("Disintegrate");
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Magic Missile
			if (target.DistanceFromPlayer > 15 && target.DistanceFromPlayer <= 50)
			{
				NewCastWizardTargetSpell("Magic Missile", target, out castSuccessfully);
				if (castSuccessfully)
				{
					D3Control.output("Magic Missile");
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Return False
            return false;
        }
		// CastForce
        bool castForce(D3Object target)
        {
            // Make sure the target is still valid
            if (!D3Control.isObjectValid(target))
			{
                return false;
			}
			// Wave of Force
			var NumWithinForceRange = D3Control.TargetManager.GetAroundEnemy(WaveOfForceDistance).Count;
			if (NumWithinForceRange > 0)
			{
				// Wave of Force
				if (D3Control.canCast("Wave of Force"))
				{
					CastWizardSpell("Wave of Force", D3Control.Player.Location);
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Explosive Blast
			if (D3Control.canCast("Explosive Blast"))
			{
				var NumWithinBlastRange = D3Control.TargetManager.GetAroundEnemy(ExplosiveBlastDistance).Count;
				if (NumWithinBlastRange > 0 && D3Control.Player.ArcanePower >= ExplosiveBlastArcanePower)
				{
					if (CastWizardSpell("Explosive Blast", D3Control.Player.Location))
					{
						Thread.Sleep(skillInterval);
						return true;
					}
				}
			}
			// Frost Nova
			if (D3Control.canCast("Frost Nova"))
			{
				// Elite Mobs
				var NumElitesWithinNovaRange = D3Control.TargetManager.GetAroundEnemy(EliteFrostNovaRange).Count;
				if (NumElitesWithinNovaRange > 0)
				{
					// Frost Nova
					if (CastWizardSpell("Frost Nova", D3Control.Player.Location))
					{
						Thread.Sleep(skillInterval);
						return true;
					}
				}
				// Normal Mobs
				var NumWithinNovaRange = D3Control.TargetManager.GetAroundEnemy(FrostNovaRange).Count;
				if (NumWithinNovaRange > 0)
				{
					if (CastWizardSpell("Frost Nova", D3Control.Player.Location))
					{
						Thread.Sleep(skillInterval);
						return true;
					}
				}
			}
			// Hydra
			if (D3Control.canCast("Hydra"))			
			{
				int curCount = D3Control.getWizHydraNumber();
				for (int j = 0; j < HydraNumber - curCount; j++)
				{
					if (CastWizardSpell("Hydra", target.Location))
					{
						Thread.Sleep(skillInterval);
						return true;
					}
				}
			}
			// Energy Twister
			if (D3Control.canCast("Energy Twister") && D3Control.Player.ArcanePower >= EnergyTwisterArcanePowerAmount)
			{
				if (CastWizardSpell("Energy Twister", target.Location))
				{
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Blizzard
			if (D3Control.canCast("Blizzard") && blizzardTimer.IsReady && D3Control.Player.ArcanePower >= BlizzardArcanePowerAmount)
			{
				if (CastWizardSpell("Blizzard", target.Location))
				{
					blizzardTimer.Reset();
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			// Meteor
			if (D3Control.canCast("Meteor") && D3Control.Player.ArcanePower >= MeteorArcanePowerAmount)
			{
				if (CastWizardSpell("Meteor", target.Location))
				{
					Thread.Sleep(skillInterval);
					return true;
				}
			}
			return false;
		}
		// Cast Defencive
        bool castDefencive(D3Object target)
        {
            // Make sure the target is still valid
            if (!D3Control.isObjectValid(target))
                return false;
			// Slow Time
			if (D3Control.canCast("Slow Time"))
			{
				if (D3Control.curTarget.IsElite)
				{
					// Elite Mobs
					var NumEliteWithinSlowTimeRange = D3Control.TargetManager.GetAroundEnemy(EliteSlowTimeDistance).Count;
					if (NumEliteWithinSlowTimeRange > 0)
					{
						if (CastWizardSpell("Slow Time", D3Control.Player.Location))
						{
							Thread.Sleep(skillInterval);
							return true;
						}
					}
				}
				else
				{
					// Normal Mobs
					var NumWithinSlowTimeRange = D3Control.TargetManager.GetAroundEnemy(SlowTimeDistance).Count;
					if (NumWithinSlowTimeRange > 0)
					{
						if (D3Control.canCast("Slow Time"))
						{
							CastWizardSpell("Slow Time", D3Control.Player.Location);
							Thread.Sleep(skillInterval);
							return true;
						}
					}
				}
			}
			// Diamond Skin
			if (D3Control.canCast("Diamond Skin"))
			{
				var Diamond30 = D3Control.TargetManager.GetAroundEnemy(DiamondSkinDistance).Count;
				if (Diamond30 > 0)
				{
					if (CastWizardSpell("Diamond Skin", D3Control.Player.Location))
					{
						Thread.Sleep(skillInterval);
						return true;
					}
				}
			}
			return false;
		}
		// This is where we check for buffs to keep up.
        public static void checkBuff(D3Player entity)
        {
            // Ice Armor
            if (!D3Control.HasBuff("Ice Armor") && D3Control.canCast("Ice Armor"))
            {
                if (CastWizardSpell("Ice Armor", D3Control.Player.Location))
                    Thread.Sleep(skillInterval);
            }
			// Storm Armor
			if (D3Control.canCast("Storm Armor") && !D3Control.HasBuff("Storm Armor"))
			{
				if (CastWizardSpell("Storm Armor", D3Control.Player.Location))
					Thread.Sleep(skillInterval);
            }
			// Energy Armor
			if (D3Control.canCast("Energy Armor") && !D3Control.HasBuff("Energy Armor"))
			{
				if (CastWizardSpell("Energy Armor", D3Control.Player.Location))
					Thread.Sleep(skillInterval);
			}
			// Magic Weapon
			if (D3Control.canCast("Magic Weapon") && MagicWeaponTimer.IsReady)
			{
				if (CastWizardSpell("Magic Weapon", D3Control.Player.Location))
				{
					MagicWeaponTimer.Reset();
					Thread.Sleep(skillInterval);				
				}
			}
			// Familiar
			if (D3Control.canCast("Familiar") && !D3Control.HasBuff("Familiar") && familiarTimer.IsReady)
			{
				if (CastWizardSpell("Familiar", D3Control.Player.Location))
				{
					familiarTimer.Reset();
					Thread.Sleep(skillInterval);
				}
			}
		}
		// Start the attack!
        void attackTarget(D3Player entity)
        {
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
			// Set the target variable as the current target set in the TargetManager
            D3Unit target = D3Control.curTarget;
			// Get target location and your location for getting sidespot for some spells.
			Vector3D location = target.Location;
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
			// If we detect a door, deal with it.
            if (D3Control.getDoorInWay(target) != null)
            {
				// Move closer to the door
				if (outputMode > 0)
                D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(target, 2.5f);      // a thread doing the move
                return;
            }
			// Avoid AOE if we need to and are set to.
            if (AvoidAoE && avoidAoE())
                return;
            // nothing in range or no LOS to target so lets move closer
            if ((!isWithinAttackDistance(target) || D3Control.LOS(target.Location)) && !target.IsDead && !D3Control.Player.isMovingForward)
            {
				//
				float d = target.DistanceFromPlayer;
				//
                if (d <= 10 && !D3Control.Player.isMovingForward)
				{
					if (D3Control.LOS(target.Location))
					{
						D3Control.output("Not in line of sight. Moving in closer.");
					}
					D3Control.MoveTo(target, d - 2.5f);
					return;
				}
				else if (!isWithinAttackDistance(target) && !D3Control.Player.isMovingForward)
				{
					if (D3Control.LOS(target.Location))
					{
						D3Control.output("Not in line of sight and outside attack distance. Moving in closer.");
					}
					D3Control.MoveTo(target, d-10);
					return;
				}
				else if (D3Control.LOS(target.Location) && !D3Control.Player.isMovingForward)
				{
					D3Control.output("Not in line of sight. Moving in closer. "+d);
					D3Control.MoveTo(target, d - 10);
					return;
				}
				else
				{
					return;
				}
                Thread.Sleep(50);
            }
			// Check if we got stuck and if we do, try to get unstuck
			if (outputMode >= 2)
				D3Control.output("Is Moving Forward Check");
            if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
            {
				AttackToGetUnstuck();
				return;
            }
			// If we are close enough to attack or if we are not moving when the moving thread is active and the target is still alive ATTACK!
            if (isWithinAttackDistance(target) || (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward && !target.IsDead))
            {
				// Get the target's HP before we start trying to kill it. This is used later to check if we didn't hurt the target and need to get unstuck or change targets
				oldTargetHP = (int)target.Hp;
				// Use the attack spells
				D3Control.output("Attacking Target ID: "+D3Control.curTarget.ID);
				castDefencive(target);
				castForce(target);
				castSecondary(target);
				castPrimary(target);
				// If the target's HP has not changed since we went through all of our spells, get a new target to attack in case we are stuck
				if (oldTargetHP == target.Hp)
				{
					D3Control.output("Target's HP has not changed, switching target. OldHP "+oldTargetHP+" CurHP"+target.Hp);
					AttackToGetUnstuck();
					pickTarget();
				}
			}
        }
		// This is used to get unstuck by using common attack spells
		void AttackToGetUnstuck()
		{
			// Mention we are stuck if outputMode is set to 1 or higher (normal output or debug)
			if (outputMode >= 1)
				D3Control.output("Cannot move forward, attacking in front of myself to clear a path.");
			// Get target's location
			Vector3D tLoc = D3Control.Player.Location;
			// Set the location to attack as in front of myself
			Vector3D location = D3Control.getTargetSideSpot(tLoc, 0, 0);
			// Energy Twister
			if (D3Control.canCast("Energy Twister"))
				CastWizardSpell("Energy Twister", location);
			// Explosive Blast
			if (D3Control.canCast("Explosive Blast"))
				CastWizardSpell("Explosive Blast", D3Control.Player.Location);
			// Frost Nova
			if (D3Control.canCast("Frost Nova"))
				CastWizardSpell("Frost Nova", D3Control.Player.Location);
		}
		// This is our function responcible for checking if safeSpot is set and is different from oldSafeSpot. If it is the case, we run from the AoE to an known safe spot.
		// Note the safeSpot is checked up on in --> public static void keepSpell()
        bool avoidAoE()
        {
			// If we have found AoE to avoid, run out of it!
            if (oldSafeSpot != safeSpot)
            {
				// Run out of the AoE!
                moverun(safeSpot);
				// Now that we have ran out of the AoE, reset the oldSafeSpot which is used to make sure we are not avoiding the same AoE too much
                oldSafeSpot = safeSpot;
				return true;
            }
			// Since we did not find any AoE to avoid, return false
            return false;
        }
		// This is used to run to an location (only used for getting out of AoE currently)
        void moverun(Vector3D location)
        {
			// If it has not been too long since we last ran away, start to run away to the set location
            if (moveBackTimer.IsReady)
            {
				// Reset our moveback timer
                moveBackTimer.Reset();
				// Click to move to an set location
				D3Control.ClickMoveTo(location);
				// While we are running, display that we are running and force us to not do anything else
				while (D3Control.isMovingWorking())
				{
					if (outputMode >= 1)
						D3Control.output("Running out of AOE");
					Thread.Sleep(100);
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
    }
}