﻿/*
	Monk Class Script by ASWeiler
    Last Edited: 1/21/2013

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
using System.Windows.Forms;

using Astronaut.Bot;
using Astronaut.Common;
using Astronaut.Scripting;
using Astronaut.Scripts.Common;
using Astronaut.D3;
using Astronaut.Monitors;
using System.Threading;
using Astronaut.Scripts;

namespace Astronaut.Scripts.Monk
{
    public class Core : GlobalBaseBotState
    {


        protected override void DoEnter(D3Player Entity)
        {
			// Set the base of DoEnter based off class
            base.DoEnter(Entity);
            // We override the default states with our own
            combatState = new Monk.CombatState();
			// Init Message
			D3Control.output("ASWeiler's Monk Class Script is now loaded.");
        }
    }
    public class CombatState : Common.CombatState
    {		
		/*
			CONFIGURABLE OPTIONS
		*/
		// [CONFIG]
		// Potion OPTIONS
        static int hpPct_UsePotion = 30;
		// Health Globe OPTIONS
		static int DistancetoHealthGlobe = 20;	// Set this to how close you want to be to the health globe to pick it up *2 for 0 pickup radius*
		static int hpPct_HealthGlobe = 60;	// Small: Heals for about 18-20%. Dropped by normal monsters mostly. || Large: Heals for about 33-35%. Dropped by champions and uniques mostly.
		static bool DashtoHealthGlobe = true;	// Set this to true if you want to use Dash twords an HP Globe (helps to get through packs you are stuck in)
		// Inner Sanctuary OPTIONS
        static int hpPct_InnerSanctuary = 50;
		// Serenity OPTIONS
		static bool useSerenityOnElites = true;	// Set this to true if you want Serenity used when an elite is the current target and is close
		static int hpPct_Serenity = 70;	// Set the percent of HP you want to cast Serenity at. (Your HP - 6202) / Your HP * 100 = hpPct_Serenity
		// Breath of Heaven OPTIONS
		static int hpPct_BreathofHeaven = 60;	// Set this to the HP % you want it to cast on to gain HP (set to 0 if you don't want it to cast on HP %)
		static bool isUsingBlazingWrath	= true;	// Set this to true if you are using the Blazing Wrath rune that needs to be re-casted every 45 seconds for the buff
		// Mantra OPTIONS
		static bool HaveSweepingWindBeforeMantraCast = true; // Set this to false if you do not want to check if Sweeping Wind is up before casting any kind of Mantra.
		static bool UseMantrasSecondaryBuff = false;	// Set this to true if you want the mantra's secondary buff to be used every x (x = the time set on MantraTimer) seconds (Good for Overawe and Mantra of Evasion's Secondary Buff)
		static CWSpellTimer MantraTimer = new CWSpellTimer(3 * 1000);	// Meant for all of the Mantra's 3 second "buffs". Set to 3 * 1000 if you want it to cast every 3 seconds. Must have UseMantrasSecondaryBuff set to true.
		static int MantraDistance = 30;	// Set this to the distance you want to make sure mobs are within before re-casting (Only works if you have UseMantrasSecondaryBuff set to true)
		static int MantraTargetsNearby = 2; // Set this to the number of mobs within MantraDistance before re-casting the (Only works if you have UseMantrasSecondaryBuff set to true)
		static int hpPct_Mantra = 50;	// Set this to the HP % you want your Mantra to be re-casted on. (Set to 0 if you do not want your Mantra to be re-cast based off your HP)
		// Overawe OPTIONS
		static bool isUsingOverawe = true;	// If you set this to true the bot will cast Mantra of Conviction every time there is more than x mobs within an x radius which is both an OPTION below.
		static bool OveraweonElitesOnly = false;	// Set this to true if you only want it to cast when an Elite is within the distance set below. (be sure to have NumofMobsNearbyToUseOverAwe set to 1 if you have this set to true)
		static int DistanceToUseOveraweOnMobs = 40;	// Set this to however close you want the mobs to be before casting Overawe (50 = One Screen)
		static int NumofMobsNearbyToUseOverAwe = 1;	// Set this to the number of mobs that have to be nearby before casting Overawe (only works if you set isUsingOverawe to true above.
		// Blinding Flash OPTIONS
		static int hpPct_BlindingFlash = 80; 	// The HP % you want to use Blinding Flash on (set to 0 if you don't want to use it on HP %)
		static int BlindingFlashDistance = 15;	// The distance the mobs need to be within before casting Blinding Flash
		static int BlindingFlashTargetsNearby = 10;	// The number of targets that has to be within BlindingFlashDistance before casting Blinding Flash
		// Cyclone Strike OPTIONS
		static int CycloneStrikeDistance = 30;	// Set this to the distance the targets must be within before casting Cyclone Strike (20 for all the runes aside from Implosion, if you are using the Imposion Rune set it to 30)
		static int CycloneStrikeTargetsNearby = 2;	// Set this to the number of targets that must be within CycloneStrikeDistance before casting Cyclone Strike
		static int CycloneStrikeSpiritCost = 50;	// Set the amount of sprit required before casting Cyclone Strike
		// Fists of Thunder OPTIONS
		static int FistsofThunderDistance = 30;	// 10 = Melee Range | 30 = Thunder Clap Rune Range
		// Seven-Sided Strike OPTIONS
		static int hpPct_SevenSidedStrike = 80; // If you do not want this to be used on a HP % based, and just have it use when there are 5 or more mobs within the rage it can hit them at, set this value to 0.
		// Dashing Strike OPTIONS
		static int DashingStrikeClosestDist = 10;	// Set this to the closest distance you want to use Dashing Strike on a target.
		static int DashingStrikeFurthestDist = 50;	// Set this to the furthest distance you want to use Dashing Strike on a target.
		static int DashingStrikeSpiritNeeded = 10; // Set this to the amount of spirit you must have before using Dashing Strike (0 = as much as possible)
		// Sweeping Wind OPTIONS
		static bool KeepSweepingWindUp = true; // Set this to true if you want to keep Sweeping Wind up to prevent losing the stacks earned. (not perfect due to we don't want to interupt TPing or IDing)
		static CWSpellTimer SweepingWindTimer = new CWSpellTimer(5400); // 5400 = 5.4 seconds between re-casting to keep Sweeping Wind up (must have KeepSweepingWindUp set to true)
		// Wave of Light OPTIONS
		static int WaveofLightDistance = 50;	// Set this to the distance that the mobs need to be within to cast Wave of Light					
		static int WaveofLightNumberofMobs = 1;	// Set this to the number of mobs that has to be within the WaveofLightDistance before casting Wave of Light
		// Focus target OPTIONS
		static bool FocusTreasureGoblin = true;		// True = focus attacking Treasure Goblin until dead
		static bool FocusMobSummoner = false;		// True = focus attacking any mob summoners until dead
		static bool FocusBloodClanSpearman = false; // True = focus the Blood Clan Spearman until dead
		static bool FocusFallenManiac = false;	// True = focus attacking the Fallen Maniac (exploding bastards) until dead 
		static bool FocusHearldofPestilence = false;	// True == focus attacking the Hearald of Pestilence until dead (poison underground guys)
		// Avoid AOE OPTIONS
		static bool AvoidAoE = false;            // try to avoid AoE (desecrate and middle of arcane beams)
		static bool DashoutofAOE = true;		// Use Dashing Strike to escape the AOE
		static int  AoESleepTime = 1000;         // time in ms to delay when moving to avoid AoE
		// Misc Settings (Avoid AOE | Focus Pack Leader | Regular and Elite Scan Distances)
		static int NumofAttacksTillChangeTarget = 3;	// Times to cast attack spells before choosing the closest target again (helps to not be stuck)
        static int RegularMobScanDistance = 40;    // attack radius for regular mobs (maximum 100 or about two screens)
        static int EliteMobScanDistance = 60;     // attack radius for elite mobs (maximum 100 or about two screens)
		static int outputMode = 1;				// 0 = Minimal Output | 1 = Normal Output | 2 = Debug Output
		// TIMERS
		static CWSpellTimer combatThresholdTimer = new CWSpellTimer(3 * 1000, false);	// Return after 10 seconds regardless
        static CWSpellTimer checkLootTimer = new CWSpellTimer(2 * 1000, false);			// Check for loot every 2 seconds
		static CWSpellTimer checkMysticAllyTimer = new CWSpellTimer(1 * 1000, false);			// Buff Check every 3 seconds
        static CWSpellTimer ExplodingPalmTimer = new CWSpellTimer(3 * 1000);			// Exploding Palm Timer
        static CWSpellTimer moveBackTimer = new CWSpellTimer(1 * 1000);					// Move Back Timer
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(100);			// Check Safe Spot Timer
		static CWSpellTimer CheckInGameTimer = new CWSpellTimer(600 * 1000);	// Check in Game Timer
		// [/CONFIG]
		/*
			END CONFIGURABLE OPTIONS
		*/
		
		/*
			ADVANCED SETTINGS (BEST NOT TO CHANGE)
		*/
		static CWSpellTimer BreathofHeavenTimer = new CWSpellTimer(45 * 1000);
		public int numofAttacks = 0;
        public static bool inited = false;	// Start with saying we have not loaded yet
		public bool enemyFound = false;
		public int oldTargetHP = 0;
		
		const int skillInterval = 100;		// Set how long we should wait after casting a spell
		//
        float range;
		//
        public int petCount = 0;
        public int petFlag = 0;
		//
        public int findModRound = 40;
		//
        public float perHit_RunBack = 20500;
		//
        static Vector3D safeSpot = null, oldSafeSpot = null;
		//
		static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();
		//
		static Thread keepSpellThread = null;
		//
        public CombatState()
        {
            petCount = D3Control.GetPetCount(0);
            petFlag = 1;

            keepSpelllStop();
            keepSpellThread = new Thread(new ThreadStart(keepSpell));
            keepSpellThread.IsBackground = true;
            keepSpellThread.Start();
        }
		//
        public static void keepSpelllStop()
        {
            try
            {
                if (keepSpellThread != null && keepSpellThread.IsAlive)
                {
                    keepSpellThread.Abort();
                }
            }
            catch
            {
            }
        }
		//
        public static void keepSpell()
        {
            bool isWorking = true;
            try
            {
                while (isWorking)
                {
					//
                    if (!D3Control.IsInGame() && CheckInGameTimer.IsReady)
					{
						CheckInGameTimer.Reset();
						return;
					}
					//
                    if (D3Control.Player.isInCombat)
                    {
                        var dTargetInfo = D3Control.getDangourousTargetInfo();
                        safeSpot = D3Control.getSafePoint(dTargetInfo);
                    }
					// Sweeping Wind up check
					if ((D3Control.canCast("Sweeping Wind") && D3Control.Player.isMovingForward) && ((D3Control.HasBuff("Sweeping Wind") && SweepingWindTimer.IsReady && KeepSweepingWindUp) || !D3Control.HasBuff("Sweeping Wind")))
					{
						if (CastMonkSpell("Sweeping Wind", D3Control.Player.Location))
							SweepingWindTimer.Reset();
					}
					// Check if we need to use a potion
					if (D3Control.Player.HpPct < hpPct_UsePotion)
						D3Control.usePotion();
                    Thread.Sleep(1000);
                }
            }
            catch
            {
            }
        }
		//
        protected override void DoEnter(D3Player Entity)
        {
			//
            if (!inited)
            {
				//
                inited = true;
				//
                setSpirit();
            }
        }
		//
        internal static void setSpirit()
        {
            if (abilityInfoDic.Count == 0)
            {
                abilityInfoDic.Add("Blinding Flash", 10);
                abilityInfoDic.Add("Lashing Tail Kick", 30);
                abilityInfoDic.Add("Exploding Palm", 40);
                abilityInfoDic.Add("Dashing Strike", 10);
                abilityInfoDic.Add("Tempest Rush", 25);
                abilityInfoDic.Add("Wave of Light", 75);
                abilityInfoDic.Add("Breath of Heaven", 25);
                abilityInfoDic.Add("Serenity", 10);
                abilityInfoDic.Add("Inner Sanctuary", 30);
                abilityInfoDic.Add("Sweeping Wind", 5);
                abilityInfoDic.Add("Mystic Ally", 25);
                abilityInfoDic.Add("Mantra of Evasion", 50);
                abilityInfoDic.Add("Mantra of Retribution", 50);
                abilityInfoDic.Add("Mantra of Healing", 50);
                abilityInfoDic.Add("Mantra of Conviction", 50);
                abilityInfoDic.Add("Seven-Sided Strike", 50);
                abilityInfoDic.Add("Cyclone Strike", 50);
            }

        }
		//
        public static bool CastMonkSpell(string SpellID, Vector3D loc)
        {
            if (hasEnoughResourceForSpell(SpellID))
			{
                if (D3Control.Player.DistanceTo(loc) >= 8 || D3Control.Player.DistanceTo(loc) <= 2 || SpellID == "Dashing Strike" || SpellID == "Tempest Rush")
                {
                    return CastMonkDirectionSpell(SpellID, loc);
                }
                return D3Control.CastLocationSpell(SpellID, loc, true);
            }
            return false;
        }
		//
        public static bool CastMonkTargetSpell(string SpellID, D3Object target)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastTargetSpell(SpellID, target);
            }
            return false;
        }
		//
        public static bool CastMonkDirectionSpell(string SpellID, Vector3D loc)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastDirectionSpell(SpellID, loc);
            }
            return false;
        }
		//
        public static bool hasEnoughResourceForSpell(string SpellID)
        {
            if (abilityInfoDic.ContainsKey(SpellID))
            {
                if (D3Control.Player.Spirit < abilityInfoDic[SpellID])
                    return false;
                else
                    return true;
            }
            else
                return true;
        }

        internal static bool isMeleeRange(D3Unit target)
        {
            // 10 feet is melee range
			// 	   	  170324 & 139454 is Act 1 Tree Normal
			//				   218947 is Elite Worm in Act 2
			//				   221291 is Act 2 Jumpers from wall in Sundered Canyon
			//				   3384 is the birds in Act 2
			//				   3037 is the summoning thing in Act 2
			//				   3349 IS BELIAL
            // SPECIAL CASES:  193077 Heart of Sin
            //                 230725 are the beasts on the side of the Rakkis Crossing bridge
            //                 96192  Siegebreaker Beast
            //                 4552   Wintersbane Stalker
            //                 60722  Demonic Tremor
            //                 149344 Unique Demonic Tremor
            //                 121353 Hulking Phasebeast
            //                 189852 Bloated Malachor
            //                 89690  Azmodan
            if (((int)target.DistanceFromPlayer <= 10) || 
				((target.ID == 170324) && ((int)target.DistanceFromPlayer <= 30)) || 
                ((target.ID == 139454) && ((int)target.DistanceFromPlayer <= 30)) || 
				((target.ID == 218947) && ((int)target.DistanceFromPlayer <= 15)) || 
				((target.ID == 221291) && ((int)target.DistanceFromPlayer <= 30)) || 
				((target.ID == 3384) && ((int)target.DistanceFromPlayer <= 30)) || 
				((target.ID == 3037) && ((int)target.DistanceFromPlayer <= 30)) || 
				((target.ID == 193077) && ((int)target.DistanceFromPlayer <= 30)) || 
                ((target.ID == 230725) && ((int)target.DistanceFromPlayer <= 13)) ||
                ((target.ID == 96192)  && ((int)target.DistanceFromPlayer <= 20)) ||
                ((target.ID == 4552)   && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 60722)  && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 149344) && ((int)target.DistanceFromPlayer <= 18)) ||
                ((target.ID == 121353) && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 189852) && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 89690)  && ((int)target.DistanceFromPlayer <= 15)))
                    return true;

            return false;
        }
		
        bool avoidAoE()
        {
			//
            if (oldSafeSpot != safeSpot)
            {
                //D3Control.output("New Safe Spot X: " + safeSpot.X + " Y: " + safeSpot.Y + " Z: " + safeSpot.Z);
                if (moveBackTimer.IsReady)
                {
					//
                    moveBackTimer.Reset();
					//
                    moverun(safeSpot);
					//
                    return true;
                }
				//
                oldSafeSpot = safeSpot;
            }
			//
            return false;
        }

        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        /// </summary>
		
        protected override void DoExecute(D3Player Entity)
        {
            // return after 10 seconds regardless
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(3 * 1000, false);
			// If we are not in game, break out of DoExecute
            if (!D3Control.IsInGame())
			{
				if (outputMode >= 2)
					D3Control.output("We are no longer in game, leaving combat mode");
				return;
			}
			// If we are dead, break out of DoExecute
            if (D3Control.Player.IsDead)
			{
				if (outputMode >= 2)
					D3Control.output("We had died, leaving combat mode.");
				return;
			}
            D3Unit originalTarget, u;
            originalTarget = D3Control.curTarget;
			// if we don't pick a target we must use the default provided else we will
			// get into a deadlock
            if (!pickTarget())
            {
                u = originalTarget;
				if (outputMode >= 2)
					D3Control.output("DoEx Orig ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                D3Control.TargetManager.SetAttackTarget(originalTarget);
            }
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(100);
				if (outputMode >= 2)
					D3Control.output("DoExe");
				// If the combat timer expires, break out of DoExecute
                if (combatThresholdTimer.IsReady)
				{
					if (outputMode >= 2)
						D3Control.output("Combat Timer Expired!");
					break;
				}
				// Update dungeon info
                D3Control.updateDungoneInfo();
				// Check For Loot
				if (checkLootTimer.IsReady)
				{
					D3Control.checkLoots();
				}
					// Keep Breath of Heaven up or use it if need be
					if (D3Control.canCast("Breath of Heaven") && ((isUsingBlazingWrath && BreathofHeavenTimer.IsReady) || D3Control.Player.HpPct < hpPct_BreathofHeaven)) 
					{
						if (CastMonkSpell("Breath of Heaven", D3Control.Player.Location))
						{
							BreathofHeavenTimer.Reset();
							Thread.Sleep(skillInterval);
						}
					}
					// Serenity
					if (D3Control.canCast("Serenity") && (D3Control.Player.HpPct < hpPct_Serenity || D3Control.Player.isBeingCCed() || (D3Control.curTarget.IsElite && useSerenityOnElites && D3Control.curTarget.DistanceFromPlayer < 20)))
					{
						CastMonkSpell("Serenity", D3Control.Player.Location);
						Thread.Sleep(skillInterval);
					}
				//
				if (outputMode >= 2)
					D3Control.output("Globe Check");
				// Grab a health globe if we need to and one exists
				if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
				{
					D3Object healthGlobe = D3Control.ClosestHealthGlobe;
					if (healthGlobe != null)
					{
						//
						if (outputMode >= 1)
							D3Control.output("Running to HP Globe");
						// If we want to and can cast the spell. Dash to the health globe
						if (DashtoHealthGlobe && D3Control.canCast("Dashing Strike"))
						{
							CastMonkTargetSpell("Dashing Strike", healthGlobe);
						}
						else
						{
							do 
							{
								// Move to the Health Globe
								D3Control.MoveTo(D3Control.ClosestHealthGlobe, DistancetoHealthGlobe);
								// Serenity based on HP Percent or we are CCd or we are set to use it before attacking an Elite and close to the Elite
								if (D3Control.canCast("Serenity") && (D3Control.Player.HpPct < hpPct_Serenity || D3Control.Player.isBeingCCed() || (D3Control.curTarget.IsElite && useSerenityOnElites && D3Control.curTarget.DistanceFromPlayer < 20)))
								{
									CastMonkSpell("Serenity", D3Control.Player.Location);
								}
								// Breath of Heaven
								if (D3Control.canCast("Breath of Heaven") && D3Control.Player.HpPct < hpPct_BreathofHeaven)
								{
									CastMonkSpell("Breath of Heaven", D3Control.Player.Location);
									BreathofHeavenTimer.Reset();
								}
								// Check if we need to use a potion
								if (D3Control.Player.HpPct < hpPct_UsePotion)
									D3Control.usePotion();
							} while (D3Control.Player.HpPct <= hpPct_HealthGlobe && !D3Control.Player.IsDead && D3Control.ClosestHealthGlobe != null);
						}
					}
				}
				if (outputMode >= 2)
					D3Control.output("Potion Check");
				// Check if we need to use a potion
				if (D3Control.Player.HpPct < hpPct_UsePotion)
					D3Control.usePotion();
				if (outputMode >= 2)
					D3Control.output("Ally Check");
				// Check if we need to re-cast buffs
				if (checkMysticAllyTimer.IsReady)
				{
					// Mystic Ally
					if (D3Control.canCast("Mystic Ally") && petFlag == 1)
					{
						if (CastMonkSpell("Mystic Ally", D3Control.Player.Location))
						{
							petCount = D3Control.GetPetCount(0);
							petFlag = 2;
						}
					}
					// Mystic Ally
					if (D3Control.canCast("Mystic Ally") && petCount != D3Control.GetPetCount(0))
					{
						if (CastMonkSpell("Mystic Ally", D3Control.Player.Location))
						{
							petCount = D3Control.GetPetCount(0);
						}
					}
				}
				// Pick target and attack the closest target if there is one to attack
				if (outputMode >= 2)
					D3Control.output("Target Check");
				if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
				{
					// make sure this function call is added if you have a while loop in DoExecute to handle the target selection.
					// it handles some boss fights, where you have to kill the minions first.
					D3Control.TargetManager.handleSpecialFight();
					doAttackSequence(D3Control.Player);
					continue;
				}
                else
                {
                    if (!pickTarget())
                    {
                        D3Control.output("No target found, returning from DoExecute.");
                        break;
                    }
                }
            }
		}

        void moveback(int length)
        {
            if (moveBackTimer.IsReady)
            {
                moveBackTimer.Reset();
                D3Control.ClickMoveTo(D3Control.getSideSpot(D3Control.Player.Location, 180, length));
                Thread.Sleep(1000);
                while (D3Control.Player.isMovingForward)
                {
                    Thread.Sleep(100);
                }
            }
        }

        void moverun(Vector3D location)
        {
			D3Control.output("Running out of AOE");
            if (moveBackTimer.IsReady)
            {
                moveBackTimer.Reset();
                float distance = D3Control.Player.DistanceTo(location);
				if (DashoutofAOE)
				{
					if (outputMode >= 1)
					{
						D3Control.output("Dashing out of AOE");
					}
					CastMonkSpell("Dashing Strike",location);
				}
				else
				{
					D3Control.ClickMoveTo(location);
					while (D3Control.isMovingWorking())
					{
						if (outputMode >= 1)
						{
							D3Control.output("Running out of AOE");
						}
						Thread.Sleep(100);
					}
				}
            }
        }
		
        void doAttackSequence(D3Player entity)
        {
			if (enemyFound && outputMode == 2)
				D3Control.output("enemyFound");
			// Set our target variable to the current target set by the Target Manager.
            D3Unit target = D3Control.curTarget;
			// BOT STATE BEFORE PULL
            if (!GlobalBaseBotState.checkBeforePull(entity))
				return;
			//
            if (AvoidAoE && avoidAoE())
                return;
			//
			if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
			{
				if (outputMode >= 1)
				{
					D3Control.output("Target Killed, going to look for next closest target");
				}
				pickTarget();
				return;
			}
			/*
				Mantras
			*/
			// If we have it set to, always be sure you have Sweeping Wind up before re-casting any Mantra related stuff
			if (D3Control.HasBuff("Sweeping Wind") && HaveSweepingWindBeforeMantraCast)
			{
				// Mantra of Evasion
						// Makes sure you can cast it first			// If we need to re-buff				// If we want to re-cast the secondary buff			// If the target is Elite and timer is ready			// If our HP is below the set amount
				if (D3Control.canCast("Mantra of Evasion") && (!D3Control.HasBuff("Mantra of Evasion") || (UseMantrasSecondaryBuff && MantraTimer.IsReady) || (D3Control.curTarget.IsElite && MantraTimer.IsReady) || D3Control.Player.HpPct < hpPct_Mantra))
				{
					if (CastMonkSpell("Mantra of Evasion", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(skillInterval);
					}
				}
				// Mantra of Retribution
				if (D3Control.canCast("Mantra of Retribution") && !D3Control.HasBuff("Mantra of Retribution"))
				{
					if (CastMonkSpell("Mantra of Retribution", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(skillInterval);
					}
				}
				// Mantra of Healing
					// Make sure you can cast it first		// Now make sure we need to even cast it to keep the buff up
				if (D3Control.canCast("Mantra of Healing") && !D3Control.HasBuff("Mantra of Healing"))
				{
					if (CastMonkSpell("Mantra of Healing", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(skillInterval);
					}
				}
				// Mantra of Conviction (Notes to be added later)
				if (D3Control.canCast("Mantra of Conviction") && (MantraTimer.IsReady || !D3Control.HasBuff("Mantra of Conviction") || target.IsElite))
				{
					if (isUsingOverawe)
					{
						if (OveraweonElitesOnly && target.IsElite)
						{
							if (CastMonkSpell("Mantra of Conviction", D3Control.Player.Location))
								MantraTimer.Reset();
							Thread.Sleep(skillInterval);
						}
						else
						{
							var MobsNearbyforOverawe = D3Control.TargetManager.GetAroundEnemy(DistanceToUseOveraweOnMobs).Count;
							if (MobsNearbyforOverawe >= NumofMobsNearbyToUseOverAwe)
							{
								if (CastMonkSpell("Mantra of Conviction", D3Control.Player.Location))
									MantraTimer.Reset();
								Thread.Sleep(skillInterval);
							}
							else if (!D3Control.HasBuff("Mantra of Conviction") || target.IsElite)
							{
								if (CastMonkSpell("Mantra of Conviction", D3Control.Player.Location))
									MantraTimer.Reset();
								Thread.Sleep(skillInterval);
							}
						}
					}
					else if (target.IsElite)
					{
						if (CastMonkSpell("Mantra of Conviction", D3Control.Player.Location))
							MantraTimer.Reset();
						Thread.Sleep(skillInterval);
					}
				}
			}
			// nothing in range or no LOS to target so lets move closer
			if (outputMode >= 2)
				D3Control.output("Is Within LOS and Close Enough To Target Check");           
            if (!isMeleeRange(target) || D3Control.pureLOS(target.Location))
            {
				if (D3Control.LOS(target.Location) || D3Control.pureLOS(target.Location))
				{
					if (outputMode >= 2)
						D3Control.output("NOT IN LOS");
				}
				else
				{
					if (outputMode >= 2)
						D3Control.output("NOT CLOSE ENOUGH");
				}
				float d = D3Control.curTarget.DistanceFromPlayer;
				if (d > 10)
				{
					D3Control.MoveTo(target, 10);
				}
				else
				{
					D3Control.MoveTo(target, d-1);
				}
                return;
            }
			// Check if we got stuck
			if (outputMode >= 2)
				D3Control.output("Is Moving Forward Check");
            if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
            {
				AttackToGetUnstuck();
				return;
            }
			// If we are close enough to attack or if we are not moving when the moving thread is active and the target is still alive ATTACK!
            if (!D3Control.pureLOS(target.Location) || target.ID == 218947)
            {
				doAttacks();
			}
			else
			{
				D3Control.output("NOT IN LOS!!!");
				return;
			}
			// If we are moving, stop moving
            if (!D3Control.isMovingWorking())
            {
                D3Control.stopMoving();
            }
        }
		void doAttacks()
		{
			// Set our target variable to the current target set by the Target Manager.
            D3Unit target = D3Control.curTarget;
			// If we are above the set max number of attacks preformed, find closest target again and reset counter
			if (numofAttacks >= NumofAttacksTillChangeTarget)
			{
				pickTarget();
				numofAttacks = 0;
			}
			oldTargetHP = (int)target.Hp;
			// Display number of attacks preformed
			if (outputMode >= 2)
				D3Control.output("Number of Attacks Preformed: "+(int)numofAttacks);
			// Tell the user that we are attacking a target if the output mode is set to 1 or higher
			if (outputMode >= 1)
				D3Control.output("Attacking Target "+target.ID+" HP:"+(int)target.Hp+" Dist:"+(int)target.DistanceFromPlayer);
			// Use AOE Spells
			if (D3Control.TargetManager.GetAroundEnemy(CycloneStrikeDistance).Count >= CycloneStrikeTargetsNearby && D3Control.Player.Spirit >= CycloneStrikeSpiritCost)
			{
				if (CastMonkSpell("Cyclone Strike",D3Control.Player.Location))
				{
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
			/*
				USE AOE SPELLS
			*/
			CastAOESpells();
			/*
				TEMPEST RUSH
			*/
			if (D3Control.canCast("Tempest Rush"))
			{
				Vector3D tLoc = D3Control.curTarget.Location;
				Vector3D location = D3Control.getTargetSideSpot(tLoc, 0, 0);
				if (D3Control.CastLocationSpell("Tempest Rush", location, true))
				{
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
			// DASHING STRIKE
			if (D3Control.canCast("Dashing Strike") && target.DistanceFromPlayer > DashingStrikeClosestDist && target.DistanceFromPlayer <= DashingStrikeFurthestDist && D3Control.Player.Spirit >= DashingStrikeSpiritNeeded)
			{
				if (CastMonkTargetSpell("Dashing Strike", target))
				{
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
			// Exploding Palm
			if (ExplodingPalmTimer.IsReady && D3Control.canCast("Exploding Palm"))
			{
				if (CastMonkTargetSpell("Exploding Palm", target))
				{
					ExplodingPalmTimer.Reset();
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
			/*
				Spirit Regenerating Attacks
			*/
			CastSpiritRegeneratingAttacks();
			//
			if (oldTargetHP == target.Hp)
			{
				D3Control.output("Target's HP has not changed, switching target. OldHP "+oldTargetHP+" CurHP"+target.Hp);
				AttackToGetUnstuck();
				pickTarget();
			}
		}
		void CastAOESpells()
		{
			//
			if (outputMode >= 2)
				D3Control.output("AOE SPELLS");
			// Set our target variable to the current target set by the Target Manager.
            D3Unit target = D3Control.curTarget;
			// Blinding Flash
			var BFmobs = D3Control.TargetManager.GetAroundEnemy(BlindingFlashDistance).Count;
			if (D3Control.canCast("Blinding Flash") && (D3Control.Player.HpPct < hpPct_BlindingFlash || BFmobs >= BlindingFlashTargetsNearby || D3Control.curTarget.IsElite))
			{
				if (CastMonkSpell("Blinding Flash", D3Control.Player.Location))
				{
					Thread.Sleep(skillInterval);
				}
			}
			// Inner Sanctuary
			var ISmobs = D3Control.TargetManager.GetAroundEnemy(20).Count;
			if (D3Control.canCast("Inner Sanctuary") && D3Control.Player.HpPct < hpPct_InnerSanctuary && ISmobs >= 1)
			{
				if (CastMonkSpell("Inner Sanctuary", D3Control.Player.Location))
				{
					Thread.Sleep(skillInterval);
				}
			}
			// Lashing Tail Kick
			var LTKmobs = D3Control.TargetManager.GetAroundEnemy(20).Count;
			if (D3Control.canCast("Lashing Tail Kick") && LTKmobs >= 1)
			{
				if (CastMonkTargetSpell("Lashing Tail Kick", target))
				{
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
			// Seven-Sided Strike
			var SSSmobs = D3Control.TargetManager.GetAroundEnemy(20).Count;
			if ((SSSmobs >= 5 || D3Control.Player.HpPct < hpPct_SevenSidedStrike) && D3Control.canCast("Seven-Sided Strike"))
			{
				if (CastMonkSpell("Seven-Sided Strike", D3Control.Player.Location))
				{
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
			// WAVE OF LIGHT
			var WOLmobs = D3Control.TargetManager.GetAroundEnemy(WaveofLightDistance).Count;
			if (D3Control.canCast("Wave of Light") && WOLmobs >= WaveofLightNumberofMobs)
			{
				if (CastMonkTargetSpell("Wave of Light", target))
				{
					Thread.Sleep(skillInterval);
					numofAttacks = numofAttacks+1;
				}
			}
		}
		void CastSpiritRegeneratingAttacks()
		{
			// Set our target variable to the current target set by the Target Manager.
            D3Unit target = D3Control.curTarget;
			// Fists of Thunder
			if (target.DistanceFromPlayer <= FistsofThunderDistance && D3Control.canCast("Fists of Thunder"))
			{
				for (int j = 0; j < 3; j++)
				{
					if (!CastMonkTargetSpell("Fists of Thunder",target))
					{
						break;
					}
					else if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
					{
						break;
					}
					numofAttacks = numofAttacks+1;
				}
			}
			// Crippling Wave
			if (D3Control.canCast("Crippling Wave"))
			{
				for (int j = 0; j < 3; j++)
				{
					if (!CastMonkTargetSpell("Crippling Wave",target))
						break;
					if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
						break;
					numofAttacks = numofAttacks+1;
				}
			}
			// Way of the Hundred Fists
			if (D3Control.canCast("Way of the Hundred Fists"))
			{
				for (int j = 0; j < 3; j++)
				{
					if (!CastMonkTargetSpell("Way of the Hundred Fists", target))
						break;
					if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
						break;
					numofAttacks = numofAttacks+1;
				}
			}
			// Deadly Reach
			if (D3Control.canCast("Deadly Reach"))
			{
				for (int j = 0; j < 3; j++)
				{
					if (!CastMonkTargetSpell("Deadly Reach", target))
						break;
					if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
						break;
					numofAttacks = numofAttacks+1;
				}
			}
		}
		void AttackToGetUnstuck()
		{
			//
			if (outputMode >= 1)
			{
				D3Control.output("Cannot move forward, attacking in front of myself to clear a path.");
			}
			// Get target's location
			Vector3D tLoc = D3Control.Player.Location;
			// Set the location to attack as in front of myself
			Vector3D location = D3Control.getTargetSideSpot(tLoc, 0, 0);
			// Fists of Thunder
			if (D3Control.canCast("Fists of Thunder"))
			{
				CastMonkSpell("Fists of Thunder",location);
			}
			// Crippling Wave
			if (D3Control.canCast("Crippling Wave"))
			{
				CastMonkSpell("Crippling Wave",location);
			}
			// Way of the Hundred Fists
			if (D3Control.canCast("Way of the Hundred Fists"))
			{
				CastMonkSpell("Way of the Hundred Fists", location);
			}
			// Deadly Reach
			if (D3Control.canCast("Deadly Reach"))
			{
				CastMonkSpell("Deadly Reach", location);
			}
		}
        bool pickTarget()
        {
            int enemyCount = 0;
            bool found = false;
            float closestEliteDistance = 100, closestMobDistance = 100;
            D3Unit closestElite = null, closestMob = null, u;

            D3Control.TargetManager.ClearTarget();

            // 65 is about one full screen length
            var mobs = D3Control.TargetManager.GetAroundEnemy(100);
            foreach (D3Unit mob in mobs)
            {
				enemyCount++;
				// Focus Treasure Seeker
				if ((mob.ID == 5985 || mob.ID == 5984 || mob.ID == 5985 || mob.ID == 5987 || mob.ID == 5988) && FocusTreasureGoblin)
				{
					if (outputMode >= 1)
						D3Control.output("Chasing Down Treasure Goblin.. Dist:" + (int)mob.DistanceFromPlayer);
					D3Control.TargetManager.SetAttackTarget(mob);
					return true;				
				}
				// Focus Mob Summoner
				if ((mob.ID == 5388 || mob.ID == 5387 || mob.ID == 4100 || mob.ID == 365 || mob.ID == 4099) && FocusMobSummoner && mob.DistanceFromPlayer <= 60)
				{
					if (outputMode >= 1)
						D3Control.output("Chasing Down Mob Summoner.. Dist: "+(int)mob.DistanceFromPlayer);
					D3Control.TargetManager.SetAttackTarget(mob);
					return true;
				}
				// Focus Blood Clan Spearman
				if (mob.ID == 4299 && FocusBloodClanSpearman && mob.DistanceFromPlayer <= 60)
				{
					if (outputMode >= 1)
						D3Control.output("Chasing Down Blood Clan Spearman.. Dist: "+(int)mob.DistanceFromPlayer);
					D3Control.TargetManager.SetAttackTarget(mob);
					return true;
				}
				// Focus Fallen Maniac
				if (mob.ID == 4095 && FocusFallenManiac && mob.DistanceFromPlayer <= 60)
				{
					if (outputMode >= 1)
						D3Control.output("Chasing Down Fallen Maniac.. Dist: "+(int)mob.DistanceFromPlayer);
					D3Control.TargetManager.SetAttackTarget(mob);
					return true;
				}
				// Focus Hearld of Pestilence
				if (mob.ID == 4738 && FocusHearldofPestilence && mob.DistanceFromPlayer <= 60)
				{
					if (outputMode >= 1)
						D3Control.output("Chasing Down Hearld of Pestilence.. Dist: "+(int)mob.DistanceFromPlayer);
					D3Control.TargetManager.SetAttackTarget(mob);
					return true;
				}
				if (!mob.IsDead && D3Control.isObjectValid(mob) && !found) 
				{
                    if (mob.DistanceFromPlayer < closestMobDistance) 
                    {
                        closestMobDistance = mob.DistanceFromPlayer;
                        closestMob = mob;
                    }
				}
            }
            // kill closest mob
            if (closestMob != null) {
                D3Control.TargetManager.SetAttackTarget(closestMob);
				u = D3Control.curTarget; 
				if (outputMode >= 2)
					D3Control.output("New Target ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                found = true;
            }
			//
            if (!found)
            {
                return false;
            }
			//
            else
            {
				if (outputMode >= 2)
					D3Control.output("# of Mobs: "+enemyCount+" Closest Mob/Elite: "+(int)closestMobDistance+"/"+(int)closestEliteDistance);
                return true;
            }
        }
        protected override void DoExit(D3Player entity)
        {
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