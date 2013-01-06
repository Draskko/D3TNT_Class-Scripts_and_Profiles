/*
	Monk Class Script by ASWeiler
    Last Edited: 1/4/2013
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
		static bool CastMantraEvery3Seconds = true;	// Set this to true if you want the mantra to be re-casted every 3 seconds (good for Overawe)
		static int MantraDistance = 30;	// Set this to the distance you want to make sure mobs are within before re-casting (Only works if you have CastMantraEvery3Seconds set to true)
		static int MantraTargetsNearby = 2; // Set this to the number of mobs within MantraDistance before re-casting the (Only works if you have CastMantraEvery3Seconds set to true)
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
		static int CycloneStrikeTargetsNearby = 3;	// Set this to the number of targets that must be within CycloneStrikeDistance before casting Cyclone Strike
		static CWSpellTimer CycloneStrikeTimer = new CWSpellTimer(3 * 1000);
		// Fists of Thunder OPTIONS
		static int FistsofThunderDistance = 30;	// 10 = Melee Range | 30 = Thunder Clap Rune Range
		// Wave of Light OPTIONS
		static int WaveofLightDistance = 30;	// Set this to the distance that the mobs need to be within to cast Wave of Light					
		static int WaveofLightNumberofMobs = 5;	// Set this to the number of mobs that has to be within the WaveofLightDistance before casting Wave of Light
		// Focus target OPTIONS
		static bool FocusTreasureGoblin = true;		// True = focus attacking Treasure Goblin until dead
		static bool FocusMobSummoner = true;		// True = focus attacking any mob summoners until dead
		// Avoid AOE OPTIONS
		static bool AvoidAoE = true;            // try to avoid AoE (desecrate and middle of arcane beams)
		static bool DashoutofAOE = true;		// Use Dashing Strike to escape the AOE
		static int  AoESleepTime = 100;         // time in ms to delay when moving to avoid AoE
		// Misc Settings (Avoid AOE | Focus Pack Leader | Regular and Elite Scan Distances)
        static int MobScanDistance = 50; // scan radius for regular mobs (maximum 100 or about two screens)
		static int outputMode = 1;				// 0 = Minimal Output | 1 = Normal Output | 2 = Debug Output
		// TIMERS
		static CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);	// Return after 10 seconds regardless
        static CWSpellTimer checkLootTimer = new CWSpellTimer(500, false);			// Check for loot every 3 seconds
		static CWSpellTimer checkMysticAllyTimer = new CWSpellTimer(1 * 1000, false);			// Buff Check every 3 seconds
        static CWSpellTimer ExplodingPalmTimer = new CWSpellTimer(3 * 1000);			// Exploding Palm Timer
        static CWSpellTimer moveBackTimer = new CWSpellTimer(1 * 1000);					// Move Back Timer
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(100);			// Check Safe Spot Timer
		static CWSpellTimer MantraTimer = new CWSpellTimer(3 * 1000);	// Meant for all of the Mantra's 3 second "buffs". Set to 3 * 1000 if you want it to cast every 3 seconds
		/*
			END CONFIGURABLE OPTIONS
		*/
		
		/*
			ADVANCED SETTINGS (BEST NOT TO CHANGE)
		*/
		static CWSpellTimer BreathofHeavenTimer = new CWSpellTimer(45 * 1000);
		public int fight_times = 12;		// Times to cast punches before checking for new targets
        public static bool inited = false;	// Start with saying we have not loaded yet
		public int meleeRange = 20;			// Set Melee Range (best not to change it)
		const int skillInterval = 50;		// Set how long we should wait after casting a spell
		
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
        static Thread keepSpellThread;
        static Vector3D safeSpot = null, oldSafeSpot = null;
		
		static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();
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
					// Make sure we are in game
					if (!D3Control.IsInGame())
					{
						return;
					}
					// Keep Breath of Heaven up or use it if need be
					if (D3Control.canCast("Breath of Heaven") && isUsingBlazingWrath && BreathofHeavenTimer.IsReady) 
					{
						if (CastMonkSpell("Breath of Heaven", D3Control.Player.Location))
						{
							BreathofHeavenTimer.Reset();
							Thread.Sleep(300);
						}
					}
					// Keep Sweeping Wind Up
                    if (D3Control.canCast("Sweeping Wind") && D3Control.Player.isInCombat)
                    {
						if( !D3Control.HasBuff("Sweeping Wind") && D3Control.Player.Spirit > 14 && D3Control.canCast("Blinding Flash") )
						{
							if (D3Control.canCast("Breath of Heaven") && isUsingBlazingWrath && BreathofHeavenTimer.IsReady) 
							{
								if (CastMonkSpell("Breath of Heaven", D3Control.Player.Location))
								{
									BreathofHeavenTimer.Reset();
									Thread.Sleep(300);
								}
							}
							CastMonkSpell("Blinding Flash", D3Control.Player.Location);
							CastMonkSpell("Sweeping Wind", D3Control.Player.Location);
							Thread.Sleep(300);
						}
						else if (!D3Control.HasBuff("Sweeping Wind") && D3Control.canCast("Sweeping Wind"))
						{
							CastMonkSpell("Sweeping Wind", D3Control.Player.Location);
							Thread.Sleep(300);
						}
                    }
					Thread.Sleep(50);
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
					D3Control.output(SpellID);
                    return CastMonkDirectionSpell(SpellID, loc);
                }
				D3Control.output(SpellID);
                return D3Control.CastLocationSpell(SpellID, loc, true);
            }
            return false;
        }
		//
        public static bool CastMonkTargetSpell(string SpellID, D3Object target)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
				D3Control.output(SpellID);
                return D3Control.CastTargetSpell(SpellID, target);
            }
            return false;
        }
		//
        public static bool CastMonkDirectionSpell(string SpellID, Vector3D loc)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
				D3Control.output(SpellID);
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
            if (((int)target.DistanceFromPlayer <= 20) || 
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

        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        /// </summary>
		
        protected override void DoExecute(D3Player Entity)
        {
            // return after 10 seconds regardless
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);
			//
			bool enemyFound = false;
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(50);
				if (outputMode > 0)
					D3Control.output("DoExe");
				// If the combat timer expires, break out of DoExecute
                if (combatThresholdTimer.IsReady)
				{
					if (outputMode > 0)
					{
						D3Control.output("Combat Timer Expired!");
					}
					break;
				}
				// If we are not in game, or are dead, or the combat timer reset, break out of DoExecute
                if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                    break;
				// Update dungeon info
                D3Control.updateDungoneInfo();
				// Check For Loot
				if (checkLootTimer.IsReady)
				{
					D3Control.checkLoots();
				}
				// Serenity based on HP Percent or we are CCd or we are set to use it before attacking an Elite and close to the Elite
				if (D3Control.canCast("Serenity") && (D3Control.Player.HpPct < hpPct_Serenity || D3Control.Player.isBeingCCed() || (D3Control.curTarget.IsElite && useSerenityOnElites && D3Control.curTarget.DistanceFromPlayer < 20)))
				{
					CastMonkSpell("Serenity", D3Control.Player.Location);
				}
				//
				if (outputMode > 1)
					D3Control.output("Globe Check");
				// Grab a health globe if we need to and one exists
				if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
				{
					D3Object healthGlobe = D3Control.ClosestHealthGlobe;
					if (healthGlobe != null)
					{
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
				if (outputMode > 1)
					D3Control.output("Potion Check");
				// Check if we need to use a potion
				if (D3Control.Player.HpPct < hpPct_UsePotion)
				{
					D3Control.usePotion();
				}
				if (outputMode > 1)
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
				if (pickTarget())
				{
					enemyFound = true;
					//
					if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
					{
						// make sure this function call is added if you have a while loop in DoExecute to handle the target selection.
						// it handles some boss fights, where you have to kill the minions first.
						D3Control.TargetManager.handleSpecialFight();
						doPulling(D3Control.Player);
					}
				}
				else
				{
					return;
				}
                if (enemyFound)
                    continue;
                if (!D3Control.Player.isInCombat || combatThresholdTimer.IsReady)
                    break;
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
            if (moveBackTimer.IsReady)
            {
                moveBackTimer.Reset();
                float distance = D3Control.Player.DistanceTo(location);
				if (DashoutofAOE)
				{
					if (outputMode > 0)
					{
						D3Control.output("Dashing out of AOE");
					}
					CastMonkSpell("Dashing Strike",location);
				}
				else
				{
					if (outputMode > 0)
					{
						D3Control.output("Running out of AOE");
					}
					D3Control.ClickMoveTo(location);
				}
                if (distance < 15)
                {
                    Thread.Sleep((int)(distance * 1000 / 22));
                }
                else
                    D3Control.Player.waitTillNoMotion();
            }
        }
		
        void doPulling(D3Player entity)
        {
			
			// Set our target variable to the current target set by the Target Manager.
            D3Unit target = D3Control.curTarget;
			// BOT STATE BEFORE PULL
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
			if (outputMode > 0)
				D3Control.output("Is Moving Forward Check");
            if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
            {
				if (outputMode > 0)
				{
					D3Control.output("Cannot move forward, changing target");
				}
				pickTarget();
				return;
            }
			if (outputMode > 0)
				D3Control.output("Is Within LOS and Close Enough To Target Check");
            // nothing in range or no LOS to target so lets move closer
            if (!isMeleeRange(target) || D3Control.LOS(target.Location))
            {
				if (D3Control.LOS(target.Location))
				{
					if (outputMode > 0)
					{
						D3Control.output("NOT IN LOS");
					}
				}
				else
				{
					D3Control.output("NOT CLOSE ENOUGH");
				}
				float d = D3Control.curTarget.DistanceFromPlayer;
				if (d > 10)
				{
					D3Control.MoveTo(target, 10);
				}
				else
				{
					D3Control.MoveTo(target, d-2);
				}
                return;
            }
			// If we are close enough to attack or if we are not moving when the moving thread is active and the target is still alive ATTACK!
            if (!D3Control.LOS(target.Location) || (target.ID == 218947 && target.DistanceFromPlayer <= 15))
            {
				// Tell the user that we are attacking a target if the output mode is set to 1 or higher
				if (outputMode > 0)
					D3Control.output("Attacking Target "+target.ID+" HP:"+(int)target.Hp+" Dist:"+(int)target.DistanceFromPlayer);
				// Use AOE Spells
				if (D3Control.TargetManager.GetAroundEnemy(CycloneStrikeDistance).Count >= CycloneStrikeTargetsNearby && CycloneStrikeTimer.IsReady)
				{
					if (CastMonkSpell("Cyclone Strike",D3Control.Player.Location))
					{
						CycloneStrikeTimer.Reset();
						return;
					}
				}
				if (D3Control.NearbyEnemyCount(35) > 1 || D3Control.curTarget.IsElite)
				{
					D3Control.output("AOE SPELLS");
					// Blinding Flash
					var mobs = D3Control.TargetManager.GetAroundEnemy(BlindingFlashDistance).Count;
					if (D3Control.Player.HpPct < hpPct_BlindingFlash || mobs >= BlindingFlashTargetsNearby || D3Control.curTarget.IsElite)
					{
						if (CastMonkSpell("Blinding Flash", D3Control.Player.Location))
						{
							return;
						}
					}
					// Inner Sanctuary
					if (D3Control.Player.HpPct < hpPct_InnerSanctuary)
					{
						if (CastMonkSpell("Inner Sanctuary", D3Control.Player.Location))
						{
							return;
						}
					}
					// Lashing Tail Kick
					if (D3Control.canCast("Lashing Tail Kick") && CastMonkTargetSpell("Lashing Tail Kick", target))
					{
						return;
					}
					//
					if (D3Control.NearbyEnemyCount(30) > 5)
					{
						if (CastMonkSpell("Seven-Sided Strike", D3Control.Player.Location))
						{
							return;
						}
					}
				}
				/*
					TEMPEST RUSH
				*/
				if (D3Control.canCast("Tempest Rush"))
				{
					Vector3D tLoc = D3Control.curTarget.Location;
					Vector3D location = D3Control.getTargetSideSpot(tLoc, 0, 0);
					if (D3Control.CastLocationSpell("Tempest Rush", location, true))
					{
						return;
					}
				}
				/*
					DASHING STRIKE
				*/
				if (D3Control.canCast("Dashing Strike") && target.DistanceFromPlayer > 0 && target.DistanceFromPlayer < 40)
				{
					if (CastMonkTargetSpell("Dashing Strike", target))
					{
						return;
					}
				}
				/*
					WAVE OF LIGHT
				*/
				var mobsWaveofLight = D3Control.TargetManager.GetAroundEnemy(WaveofLightDistance).Count;
				if (mobsWaveofLight >= WaveofLightNumberofMobs)
				{
					if (CastMonkTargetSpell("Wave of Light", target))
					{
						return;
					}
				}
				/*
					FISTS OF THUNDER
				*/
				//
				if (target.DistanceFromPlayer <= FistsofThunderDistance && D3Control.canCast("Fists of Thunder"))
				{
					for (int j = 0; j < 3; j++)
					{
						CastMonkTargetSpell("Fists of Thunder",target);
						if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
						{
							if (outputMode > 0)
							{
								D3Control.output("Killed Target With Fists of Thunder");
							}
							return;
						}
					}
				}
				//
				if (target.IsElite && (target.DistanceFromPlayer < 20 || target.DistanceFromPlayer < meleeRange))
				{
					if (ExplodingPalmTimer.IsReady && CastMonkTargetSpell("Exploding Palm", target))
					{
						ExplodingPalmTimer.Reset();
						return;
					}
					if (CastMonkTargetSpell("Wave of Light", target))
						return;
				}
				//
				else
				{
					if (!D3Control.canCast("Way of the Hundred Fists") && D3Control.canCast("Crippling Wave"))
					{
						for (int j = 0; j < 3; j++)
						{
							if (!CastMonkTargetSpell("Crippling Wave", target))
								break;
							D3Control.Player.Wait(skillInterval + 100);
							if (!D3Control.isObjectValid(target) || target.IsDead)
								return;
						}
					}
				}
				//
				if (target.IsElite && target.MLevel != 3 && (target.DistanceFromPlayer <= meleeRange || target.DistanceFromPlayer <= 20))
				{
					if (CastMonkSpell("Seven-Sided Strike", D3Control.Player.Location))
					{
						return;
					}
				}
				for (int j = 0; j < 3; j++)
				{
					if (true)
					{
						if (!CastMonkTargetSpell("Way of the Hundred Fists", target))
							break;
					}
					if (!D3Control.isObjectValid(target) || target.IsDead)
						break;
				}
				//
				if (target.DistanceFromPlayer < 20 && D3Control.canCast("Deadly Reach"))
				{
					for (int j = 0; j < 3; j++)
					{
						if (true)
						{
							if (!CastMonkTargetSpell("Deadly Reach", target))
								break;
						}
						if (!D3Control.isObjectValid(target) || target.IsDead)
							break;
					}
				}
			}
			else
			{
				D3Control.output("NOT IN LOS");
				return;
			}
            if (!D3Control.isMovingWorking())
            {
                D3Control.stopMoving();
            }
        }
        void pickTarget()
        {
            int enemyCount = 0;
            bool found = false;
            float closestMobDistance = 50;
            D3Unit closestMob = null, u;

            D3Control.TargetManager.ClearTarget();

            // 65 is about one full screen length
            var mobs = D3Control.TargetManager.GetAroundEnemy(MobScanDistance);
            foreach (D3Unit mob in mobs)
            {
				if (!D3Control.LOS(mob.Location))
                {
					enemyCount++;
					if (!mob.IsDead && !D3Control.LOS(mob.Location) && !found) 
					{
						if (mob.DistanceFromPlayer < closestMobDistance)
						{
							closestMobDistance = mob.DistanceFromPlayer;
							closestMob = mob;
						}
					}
				}
            }
            // kill all trash mobs within RegularMobScanDistance and elites within EliteMobScanDistance
            if ((closestMobDistance <= MobScanDistance) && (closestMob != null)) {
                D3Control.TargetManager.SetAttackTarget(closestMob);
                found = true;
            }
			//
            if (!found)
            {
                return 0;
            }
			//
            else
            {
                u = D3Control.curTarget;
                D3Control.output("New Target ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                return closestMobDistance;
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