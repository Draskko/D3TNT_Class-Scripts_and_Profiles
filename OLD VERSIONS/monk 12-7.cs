/*
	Monk Class Script by ASWeiler
    Last Edited: 12/4/2012
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
            // Init Message
            D3Control.output("Class Script by ASWeiler Loaded");
            // We override the default states with our own
            combatState = new Monk.CombatState();
        }
    }
    public class CombatState : Common.CombatState
    {
        static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();
		
		/*
			CONFIGURABLE SETTINGS
		*/
		// HP % Related Settings (Use of Potion, Run to HP Globe, Mantra use, Breath of Heaven, Inner Sanctuary, Blinding Flash, Serenity.
        public int hpPct_UsePotion = 50;		
		public int hpPct_HealthGlobe = 70;
        public int hpPct_Mantra = 80;
        public int hpPct_InnerSanctuary = 50;
		public int hpPct_BlindingFlash = 60; 
        public int hpPct_Serenity = 80;
		public int hpPct_BreathofHeaven = 100;	// Set me to 100 to make it cast every time we engage in a fight
		// Wave of Light OPTIONS
		public int WaveofLightCastRange = 30;											
		public int WaveofLightNumberofMobs = 5;
		// Focus target OPTIONS
		static bool FocusPackLeader = false;     	// True = focus target elite pack leader until dead
		static bool FocusTreasureGoblin = true;		// True = focus attacking Treasure Goblin until dead
		static bool FocusMobSummoner = true;		// True = focus attacking any mob summoners until dead
		// Avoid AOE OPTIONS
		static bool AvoidAoE = true;            // try to avoid AoE (desecrate and middle of arcane beams)
		// Misc Settings (Avoid AOE | Focus Pack Leader | Regular and Elite Scan Distances)
        static int RegularMobScanDistance = 60; // scan radius for regular mobs (maximum 100 or about two screens)
		static int ouputMode = 1;				// 0 = Minimal Output | 1 = Normal Output | 2 = Debug Output
		// TIMERS
		static CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);	// Return after 10 seconds regardless
        static CWSpellTimer checkLootTimer = new CWSpellTimer(1 * 1000, false);			// Check for loot every 3 seconds
		static CWSpellTimer checkBuffTimer = new CWSpellTimer(3 * 1000, false);			// Buff Check every 3 seconds
		static CWSpellTimer MantraTimer = new CWSpellTimer(3 * 1000);					// Timer for use of Mantra vs Elites
        static CWSpellTimer ExplodingPalmTimer = new CWSpellTimer(3 * 1000);			// Exploding Palm Timer
        static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000);					// Move Back Timer
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1 * 1000);			// Check Safe Spot Timer
		/*
			END CONFIGURABLE SETTINGS
		*/
		
		/*
			ADVANCED SETTINGS (BEST NOT TO CHANGE)
		*/
		public int fight_times = 12;		// Times to cast punches before checking for new targets
        public static bool inited = false;	// Start with saying we have not loaded yet
		public int meleeRange = 10;			// Set Melee Range (best not to change it)
		const int skillInterval = 10;		// Set how long we should wait after casting a spell
		
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
        static Vector3D safeSpot = null;
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
                    if (checkSafeSpotTimer.IsReady && D3Control.Player.isInCombat)
                    {
                        checkSafeSpotTimer.Reset();
                        var dTargetInfo = D3Control.getDangourousTargetInfo();
                        safeSpot = D3Control.getSafePoint(dTargetInfo);
                    }
                    Thread.Sleep(300);
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
                if (D3Control.Player.DistanceTo(loc) >= 8 || D3Control.Player.DistanceTo(loc) <= 2 || SpellID == "Dashing Strike")
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
				((target.ID == 218947) && ((int)target.DistanceFromPlayer <= 30)) || 
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
                enemyCount++;
                if (!mob.IsDead && !D3Control.LOS(mob.Location) && !found) 
                {
                    if(!mob.IsElite && (mob.DistanceFromPlayer < closestMobDistance)) 
                    {
                        closestMobDistance = mob.DistanceFromPlayer;
                        closestMob = mob;
                    }
 
                    if (mob.IsElite && (mob.DistanceFromPlayer < closestEliteDistance)) 
                    {
                        closestEliteDistance = mob.DistanceFromPlayer;
                        closestElite = mob;
                    }
					// Focus Treasure Seeker
					if ((mob.ID == 5985 || mob.ID == 5984 || mob.ID == 5985 || mob.ID == 5987 || mob.ID == 5988) && FocusTreasureGoblin)
					{
						if (ouputMode > 0)
							D3Control.output("Found Treasure Goblin (ID: " + mob.ID + " Dist: " + (int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;					
					}
					// Focus Mob Summoner
					if ((mob.ID == 5388 || mob.ID == 5387) && FocusMobSummoner)
					{
						if (ouputMode > 0)
							D3Control.output("Found Mob Summoner ID: "+mob.ID+" Dist: "+(int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;
					}
					// Focus Pack Leader
                    if ((mob.MLevel == 2) && FocusPackLeader)
                    {
						if (ouputMode > 0)
							D3Control.output("Found Pack Leader ID: "+mob.ID+" Dist: "+(int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;
                    }
                }
            }

            //D3Control.output("DoExecute Cnt: " + enemyCount + " cElite:" + (int)closestEliteDistance + " cMob: " + (int)closestMobDistance);

            // kill all trash mobs within RegularMobScanDistance and elites within EliteMobScanDistance
            if ((closestMobDistance <= RegularMobScanDistance) && (closestMob != null)) {
                D3Control.TargetManager.SetAttackTarget(closestMob);
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
                u = D3Control.curTarget;
                //D3Control.output("New Target ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                return true;
            }
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
            D3Unit originalTarget, u;
            originalTarget = D3Control.curTarget;
			//
            if (!pickTarget())
            {
                // if we don't pick a target we must use the default provided else we will
                // get into a deadlock
                u = originalTarget;
                //D3Control.output("DoEx Orig ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                D3Control.TargetManager.SetAttackTarget(originalTarget);
            }
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(50);
				// If the combat timer expires, break out of DoExecute
                if (combatThresholdTimer.IsReady)
				{
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
					D3Control.usePotion();
				// Check For Loot
                if (checkLootTimer.IsReady)
                {
                    D3Control.checkLoots();
                }
				// Check if we need to re-cast buffs
				if (checkBuffTimer.IsReady)
				{
					checkBuff(Entity);
				}
				// Escape AOE Check
				if (AvoidAoE)
					try2AvoidAOE();
				// Do Pulling if we got a target to kill!
                if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                {
                    D3Control.TargetManager.handleSpecialFight();
                    doPulling(D3Control.Player);
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

        bool try2AvoidAOE()
        {
            if (safeSpot != null && moveBackTimer.IsReady)
            {
                var tempSpot = safeSpot;
                safeSpot = null;
                D3Control.output("AOE detected, try to run away..");
                DateTime start = DateTime.Now;
                moverun(tempSpot);
                D3Control.output(string.Format("{0}", DateTime.Now.Subtract(start).TotalMilliseconds));
                D3Control.output("Running away done");
                return true;
            }
            return false;
        }

        void moverun(Vector3D location)
        {
            if (moveBackTimer.IsReady)
            {
                moveBackTimer.Reset();
                float distance = D3Control.Player.DistanceTo(location);
                D3Control.ClickMoveTo(location);
                if (distance < 15)
                {
                    Thread.Sleep((int)(distance * 1000 / 22));
                }
                else
                    D3Control.Player.waitTillNoMotion();
            }
        }

        static bool shouldCastSS()
        {
            return !D3Control.HasBuff("Sweeping Wind");
        }
		
        void handleAOE()
        {
			D3Object target = D3Control.curTarget;
			if (D3Control.NearbyEnemyCount(50) > 1 || D3Control.curTarget.IsElite)
			{
				// Blinding Flash
				var mobs = D3Control.TargetManager.GetAroundEnemy(40).Count;
				if (D3Control.Player.HpPct < hpPct_BlindingFlash || mobs > 10 || D3Control.curTarget.IsElite)
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
				//
                if (D3Control.NearbyEnemyCount(30) > 5)
                {
                    if (CastMonkSpell("Seven-Sided Strike", D3Control.Player.Location))
                    {
						return;
					}
                }
				//
                if (D3Control.canCast("Crippling Wave") && target.DistanceFromPlayer < meleeRange)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (!CastMonkSpell("Crippling Wave", D3Control.getSideSpot(D3Control.Player.Location, 0, 5)))
                        { 
							break;
						} 
                        D3Control.Player.Wait(skillInterval + 100);
                        if (!D3Control.isObjectValid(target))
                            return;
                    }
                }
                for (int j = 0; j < 3; j++)
                {
                    if (!CastMonkTargetSpell("Deadly Reach", target))
                    { 
						break;
					} 
                    if (!D3Control.isObjectValid(target))
                        break;
                }
                for (int j = 0; j < 3; j++)
                {
                    if (!CastMonkTargetSpell("Way of the Hundred Fists", target))
                        break;
                    if (!D3Control.isObjectValid(target))
                        break;
                }
            }
        }
		//
        public void checkBuff(D3Player entity)
        {
			// Serenity
            if (D3Control.Player.isBeingCCed())
            {
                if (D3Control.canCast("Serenity"))
                {
                    if (CastMonkSpell("Serenity", D3Control.Player.Location))
                    {
						return;
					}
                }
            }
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
            return;
        }

        void doPulling(D3Player entity)
        {
			// Set the target to the cloest enemy
			//D3Object closesttarget = D3Control.ClosestEnemy;
			//D3Control.TargetManager.SetAttackTarget((D3Unit)closesttarget);
			// Set our target variable to the current target set by the Target Manager.
            D3Unit target = D3Control.curTarget;
			// Make sure the target is still valid and alive
			if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
			// BOT STATE BEFOR PULL
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
			// Check if we need to use a potion
			if (D3Control.Player.HpPct < hpPct_UsePotion)
				D3Control.usePotion();
			// Check if we need to re-cast buffs
			if (checkBuffTimer.IsReady)
			{
				checkBuff(entity);
			}
			// Escape AOE Check
			if (AvoidAoE && try2AvoidAOE())
				return;
			// If we detect a door, deal with it.
            if (D3Control.getDoorInWay(target) != null)
            {
				// Move closer to the door
                D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(target, 2.5f);      // a thread doing the move
                return;
            }

			// If we are close enough to attack or if we are not moving when the moving thread is active and the target is still alive ATTACK!
            if (!D3Control.LOS(target.Location))
            {
                //D3Control.output("Attacking Target "+target.ID+" HP:"+(int)target.Hp);
				// Make sure Sweeping Wind is up
				if (shouldCastSS() && D3Control.canCast("Sweeping Wind"))
				{
					if (D3Control.canCast("Blinding Flash"))
					{
						CastMonkSpell("Blinding Flash", D3Control.Player.Location);
					}
					CastMonkSpell("Sweeping Wind", D3Control.Player.Location);
					Thread.Sleep(skillInterval);
				}
				/*
					Healing Spells
				*/
				// Serenity
				if (D3Control.Player.HpPct < hpPct_Serenity)
				{
					if (CastMonkSpell("Serenity", D3Control.Player.Location))
					{
						return;
					}
				}
				// Breath of Heaven
				if (D3Control.Player.HpPct < hpPct_BreathofHeaven || hpPct_BreathofHeaven > 99)
				{
					if (CastMonkSpell("Breath of Heaven", D3Control.Player.Location))
					{
						return;
					}
				}
				/*
					TEMPEST RUSH
				*/
				Vector3D tLoc = D3Control.curTarget.Location;
				Vector3D location = D3Control.getTargetSideSpot(tLoc, 0, 0);
				if (D3Control.CastLocationSpell("Tempest Rush", location, true))
				{
					return;
				}
				/*
					Target Spells
				*/
				// Make sure the target is still valid and alive
				if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
				}
				if (D3Control.canCast("Mantra of Evasion"))
				{
					if ((!D3Control.HasBuff("Mantra of Evasion") || (D3Control.curTarget.IsElite && MantraTimer.IsReady && D3Control.HasBuff("Sweeping Wind")) || (D3Control.Player.HpPct < hpPct_Mantra && D3Control.HasBuff("Sweeping Wind"))) && CastMonkSpell("Mantra of Evasion", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(200);
					}
				} 
				else if (D3Control.canCast("Mantra of Retribution"))
				{
					if ((!D3Control.HasBuff("Mantra of Retribution") || (target.IsElite && MantraTimer.IsReady && D3Control.HasBuff("Sweeping Wind")) || (D3Control.Player.HpPct < hpPct_Mantra && D3Control.HasBuff("Sweeping Wind"))) && CastMonkSpell("Mantra of Retribution", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(200);
					}
				}
				else if (D3Control.canCast("Mantra of Healing"))
				{
					if ((!D3Control.HasBuff("Mantra of Healing") || (target.IsElite && MantraTimer.IsReady && D3Control.HasBuff("Sweeping Wind")) || (D3Control.Player.HpPct < hpPct_Mantra && D3Control.HasBuff("Sweeping Wind"))) && CastMonkSpell("Mantra of Healing", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(200);
					}
				}
				else if (D3Control.canCast("Mantra of Conviction"))
				{
					if ((!D3Control.HasBuff("Mantra of Conviction") || (target.IsElite && MantraTimer.IsReady && D3Control.HasBuff("Sweeping Wind"))|| (D3Control.Player.HpPct < hpPct_Mantra && D3Control.HasBuff("Sweeping Wind"))) && CastMonkSpell("Mantra of Conviction", D3Control.Player.Location))
					{
						MantraTimer.Reset();
						Thread.Sleep(200);
					}
				}
				//
				if (D3Control.canCast("Dashing Strike") && target.DistanceFromPlayer > 15 && System.Math.Abs(target.Location.Z - D3Control.Player.Location.Z) < 3)
				{
					if (CastMonkTargetSpell("Dashing Strike", target))
					{
						if (!D3Control.isMovingWorking())
						{
							D3Control.stopMoving();
						}
					}
				}
				// Wave of Light
				var mobsWaveofLight = D3Control.TargetManager.GetAroundEnemy(WaveofLightCastRange).Count;
				if (mobsWaveofLight > WaveofLightNumberofMobs)
				{
					if (CastMonkTargetSpell("Wave of Light", target))
					{
						return;
					}
				}
				// Make sure the target is still valid and alive
				if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
				}
				//
				if (D3Control.NearbyEnemyCount(35) > 0)
				{

					if (CastMonkSpell("Blinding Flash", D3Control.Player.Location))
					{
						return;
					}
					if (CastMonkTargetSpell("Lashing Tail Kick", target))
					{
						return;
					}
				}
				handleAOE();
				// Make sure the target is still valid and alive
				if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
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
				if (target.DistanceFromPlayer < 20)
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
				//
				if (target.DistanceFromPlayer <= 30)
				{
					for (int j = 0; j < 3; j++)
					{
						if (!CastMonkTargetSpell("Fists of Thunder", target))
							break;
						if (!D3Control.isObjectValid(target) || target.IsDead)
							break;
					}
				}
			}
			// Make sure the target is still valid and alive
			if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
            if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward && !target.IsDead)
            {
                if (D3Control.canCast("Crippling Wave") && target.DistanceFromPlayer < meleeRange)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (!CastMonkSpell("Crippling Wave", D3Control.getSideSpot(D3Control.Player.Location, 0, 5)))
                        { 
							break;
						} 
                        D3Control.Player.Wait(skillInterval + 100);
                        if (!D3Control.isObjectValid(target))
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
				if (target.DistanceFromPlayer < 20)
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
				//
				for (int j = 0; j < 3; j++)
				{
					if (true)
					{
						if (!CastMonkTargetSpell("Fists of Thunder", target))
							break;
					}
					if (!D3Control.isObjectValid(target) || target.IsDead)
						break;
				}
				// Wave of Light
				if (D3Control.canCast("Wave of Light"))
				{
					if (CastMonkTargetSpell("Wave of Light", target))
					{
						return;
					}
				}
            }
			// Make sure the target is still valid and alive
			if (target == null || !D3Control.isObjectValid(target) || target.IsDead || target.Hp <= 0 || target.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
            // nothing in range or no LOS to target so lets move closer
            if (!isMeleeRange(target) || D3Control.LOS(target.Location))
            {
                D3Control.MoveTo(target, 10);
                Thread.Sleep(50);
            }
			return;
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