/*
	Barbarian Class Script by ASWeiler
    Last Edited: 12/1/2012
    Thanks To: D3TNT for AOE Avoid and original Core.cs and Narbo for his Barbarian Class Script.

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

using Astronaut.Bot;
using Astronaut.Common;
using Astronaut.Scripting;
using Astronaut.Scripts.Common;
using Astronaut.D3;
using Astronaut.Monitors;
using System.Threading;
using Astronaut.Scripts;

namespace Astronaut.Scripts.Barbarian
{
    public class Core : GlobalBaseBotState
    {
        public string coreVersion = "v3.0";        
        protected override void DoEnter(D3Player Entity)
        {
            base.DoEnter(Entity);
            D3Control.output("ASWeiler's Barbarian Script" + coreVersion); 
            combatState = new Barbarian.CombatState();
            D3Control.output("Thanks to Narbo for his Barbarian Script.");
        }

        protected override bool IsHealer()
        {
            return false;
        }

        public static void Emergency(D3Player player) { }

        protected void CastInterruption() { }

        public static void DebuffAll() { }

        public override bool NeedRest(D3Player player)
        {
            return false;
        }
    }

    public class CombatState : Common.CombatState
    {
        /* CONFIGURATION OPTIONS */
        static int  HealthPotHpPct = 60;            // below this % health the script will try to use a health potion
        static bool moveForHealthGlobe = true;      // move to pick up nearby healthGlobes
        static int  HealthGlobeHpPct = 70;          // below this % the script will try to obtain nearby health globes
        static int  RegularMobScanDistance = 40;    // attack radius for regular mobs (maximum 100 or about two screens)
        static int  EliteMobScanDistance = 40;     // attack radius for elite mobs (maximum 100 or about two screens)
        static int  ActionDelay = 150;              // time in ms to sleep after using a skill
        static bool AvoidAoE = true;                // try to avoid AoE (desecrate and middle of arcane beams)
        static int  AoESleepTime = 750;             // time in ms to delay when moving to avoid AoE
        static bool FocusPackLeader = true;        // focus target elite pack leader (pre 1.0.4 invuln minion workaround)
		static bool FocusTreasureGoblin = true;	// If this is set to true the bot will focus targeting the treasure goblin till dead
        static int  RendTime = 2500;                // time in milliseconds between uses of Rend (Fury permitting)
        static bool useLeapHealthGlobe = true;     // EXPERIMENTAL:  Leap to health globes when life % is below HealthGlobeHpPct
        static bool enableSaveFury = false;          // when fighting elites save fury for WoTB/Earthquake/Call of Ancients
		static int SprintFuryAmount = 30;			// The amount of fury needed before trying to use Sprint.
        static int  saveFuryPct = 55;               // percent of fury to save vs elites, above this value we will can use fury as normal
        static int  ignorePainPct = 50;             // below this % life the script will try to use ignore pain (if available)
		// Whirlwind Options
		static int WhirlWindFuryAmount = 10;		// The amount of fury needed before trying to use WhirlWind.
		static int WhirlwindRange = 50;				// The range in which the whirlwind will be used in. (50 = one screen length)
		static int NumofEnemiesInWhirlwindRange = 3;	// The number of enemies that must be within Whirlwind Range before using Whirlwind
        /* END CONFIGURATION OPTIONS */

        static Thread keepSpellThread = null;
        static Vector3D safeSpot = null, oldSafeSpot = null;
        static int targetsInRange20 = 0, targetsInRange15 = 0, targetsInRange10 = 0, targetsInRange8 = 0;
        static int LastValidHpPct = 100;
        static CWSpellTimer rendTimer = new CWSpellTimer(RendTime);
        static CWSpellTimer moveBackTimer = new CWSpellTimer(5 * 1000);
		static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1 * 1000);	// Checking for safe spot timer (default 1 sec)
        static CWSpellTimer checkLootTimer = new CWSpellTimer(1 * 1000, false);
        static bool saveFury = false;
        #region ability rage
        static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();
        #endregion

        public static bool inited = false;

        public CombatState()
        {
            try 
            {
                if ((keepSpellThread != null) && keepSpellThread.IsAlive)
                {
                    keepSpellThread.Abort();
                    keepSpellThread.Join();
                }
            }
            catch (ThreadStateException e) 
            {
            }

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

            D3Control.output("Starting spell thread. Seed: " + seed);

            try
            {
                while (true)
                {
                    if (!D3Control.IsInGame() || (D3Control.playerClass != (int)D3Control.ToonClass.Barbarian))
                    {
                       D3Control.output("Aborting spell thread! Seed: " + seed);             
                       return;
                    }
                    targetsInRange20 = 0;
                    targetsInRange15 = 0;
                    targetsInRange10 = 0;
                    targetsInRange8  = 0;
                    var enemies = D3Control.TargetManager.GetAroundEnemy(20);
                    foreach (D3Unit enemy in enemies)
                    {
                        if ((int)enemy.DistanceFromPlayer <= 20)
                           targetsInRange20++;

                        if ((int)enemy.DistanceFromPlayer <= 15)
                           targetsInRange15++;

                        if ((int)enemy.DistanceFromPlayer <= 10)
                           targetsInRange10++;

                        if ((int)enemy.DistanceFromPlayer <= 8)
                           targetsInRange8++;

                        //D3Control.output("TargetList ID: " + enemy.ID + " Dist: " + (int)enemy.DistanceFromPlayer + " ML: " + enemy.MLevel);
                    }

                    // D3Control.output("Count: " + count + " Enemies: " + targetsInRage);

					// Check for AOE to avoid
                    if (checkSafeSpotTimer.IsReady)
                    {
                        checkSafeSpotTimer.Reset();
                        var dTargetInfo = D3Control.getDangourousTargetInfo();
                        safeSpot = D3Control.getSafePoint(dTargetInfo);
                    }


                    // War Cry will generate fury for Sprint
                    if (D3Control.canCast("War Cry") && !D3Control.HasBuff("Sprint") && D3Control.isMovingWorking())
                    {
                        CastBarbarianSpell("War Cry", D3Control.Player.Location);
                        Thread.Sleep(ActionDelay);
                    }
					var nearbyenemies = D3Control.TargetManager.GetAroundEnemy(50).Count;
                    if (D3Control.canCast("Sprint") && !D3Control.HasBuff("Sprint") && D3Control.isMovingWorking())
                    {
                        D3Unit u = D3Control.curTarget;

                        if (D3Control.isObjectValid(u) && !u.IsDead && enableSaveFury && (D3Control.Player.Fury <= (saveFuryPct + 15)) && D3Control.isOnCD("Sprint"))
                        {
                            // don't sprint in combat until we have saved fury
                        }
						else if (D3Control.Player.Fury >= SprintFuryAmount)
                        {
                            CastBarbarianSpell("Sprint", D3Control.Player.Location);
                            Thread.Sleep(ActionDelay);
                        }
                    }
                    Thread.Sleep(300);
                }
            }
            catch
            {
            }
        }

        protected override void DoEnter(D3Player Entity)
        {
            if (!inited)
            {
                inited = true;
                setFury();
            }
        }

        internal static void setFury()
        {
            if (abilityInfoDic.Count == 0)
            {
                abilityInfoDic.Add("Hammer of the Ancients", 20);
                abilityInfoDic.Add("Siesmic Slam", 30);
                abilityInfoDic.Add("Rend", 20);
                abilityInfoDic.Add("Battle Rage", 20);
                abilityInfoDic.Add("Sprint", 20);
                abilityInfoDic.Add("Whirlwind", 16);
                abilityInfoDic.Add("Weapon Throw", 10);
                abilityInfoDic.Add("Earthquake", 50);
                abilityInfoDic.Add("Call of the Ancients", 50);
                abilityInfoDic.Add("Wrath of the Berserker", 50);
            }

        }

        public static bool CastBarbarianSpell(string SpellName, Vector3D loc)
        {
            bool ret = false;

            if (hasEnoughResourceForSpell(SpellName))
            {
                float distance = D3Control.Player.DistanceTo(loc);

                if ((distance > 2 && distance < 10) || SpellName == "LeapAttack" || SpellName == "Furious Charge" || SpellName == "Weapon Throw" || SpellName == "Ancient Spear")
                {
                    ret = D3Control.CastLocationSpell(SpellName, loc, true);
					Thread.Sleep(50);
                }
				else if (SpellName == "Whirlwind" && distance < 50)
				{
					ret = D3Control.CastLocationSpell(SpellName, loc, true);
					Thread.Sleep(50);
				}
                else
				{
                    ret = D3Control.CastDirectionSpell(SpellName, loc);
					Thread.Sleep(50);
				}
            }

            if (ret)
            {
                if ((SpellName == "Call of the Ancients") || (SpellName == "Earthquake") || (SpellName == "Wrath of the Berserker"))
                {
                    D3Control.output("DEACTIVATE SAVE FURY");
                    saveFury = false;
                }

                Thread.Sleep(ActionDelay);
            }
            else
                Thread.Sleep(ActionDelay/2);

            return ret;
        }

        public static bool CastBarbarianDirectionSpell(string SpellName, Vector3D loc)
        {
            if (hasEnoughResourceForSpell(SpellName))
            {
                return D3Control.CastDirectionSpell(SpellName, loc);
            }
            return false;
        }

        public static bool hasEnoughResourceForSpell(string SpellName)
        {
            if (abilityInfoDic.ContainsKey(SpellName))
            {
                if (enableSaveFury && (D3Control.Player.Fury <= saveFuryPct) && saveFury)
                    return false;
                
                if (D3Control.Player.Fury < abilityInfoDic[SpellName])
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
                ((target.ID == 193077) && ((int)target.DistanceFromPlayer <= 30)) || 
                ((target.ID == 230725) && ((int)target.DistanceFromPlayer <= 13)) ||
                ((target.ID == 96192)  && ((int)target.DistanceFromPlayer <= 20)) ||
                ((target.ID == 4552)   && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 60722)  && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 149344) && ((int)target.DistanceFromPlayer <= 18)) ||
                ((target.ID == 121353) && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 189852) && ((int)target.DistanceFromPlayer <= 15)) ||
                ((target.ID == 89690)  && ((int)target.DistanceFromPlayer <= 45)))
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
            var mobs = D3Control.TargetManager.GetAroundEnemy(100);
            foreach (D3Unit mob in mobs) 
            {
				if (mob.Untargetable)
				{
					D3Control.output("The target is Untargetable, skipping.");
					break;
				}
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

                    // SPECIAL CASE 1:
                    // Prioritize the Heart of Sin over all other targets
                    //if (mob.ID == 193077)
                    //{
                    //    D3Control.output("Prioritizing Heart of Sin Dist: " + (int)mob.DistanceFromPlayer);
                    //    D3Control.TargetManager.SetAttackTarget(mob);
                    //    return true;
                    //}
					
					// Focus Treasure Goblin if set to do so
					if ((mob.ID == 5985 || mob.ID == 5984 || mob.ID == 5985 || mob.ID == 5987 || mob.ID == 5988) && FocusTreasureGoblin)
					{
                        D3Control.output("Found Treasure Goblin (ID: " + mob.ID + " Dist: " + (int)mob.DistanceFromPlayer+")");
                        D3Control.TargetManager.SetAttackTarget(mob);
                        return true;					
					}

                    // SPECIAL CASE 2:  MLevel 2 is a leader (with minions)
                    // since 1.0.4 there are no invuln minions so no need for this code
                    // however we keep it as an option for people that like this behavior
                    if ((mob.MLevel == 2) && FocusPackLeader)
                    {
                        D3Control.output("Found Pack Leader ID: " + mob.ID + " Dist: " + (int)mob.DistanceFromPlayer + " ML: " + mob.MLevel);
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

            if ((closestEliteDistance <= EliteMobScanDistance) && (closestElite != null)) {
                D3Control.TargetManager.SetAttackTarget(closestElite);
                found = true;
				
				if (enableSaveFury)
				{
					if ((D3Control.canCast("Wrath of the Berserker") && D3Control.isOnCD("Wrath of the Berserker")) ||
						(D3Control.canCast("Earthquake") && D3Control.isOnCD("Earthquake")) ||
						(D3Control.canCast("Call of the Ancients") && D3Control.isOnCD("Call of the Ancients")))
					{
						D3Control.output("ACTIVATE SAVE FURY");
						saveFury = true;
					}
				}
            }

            if (!found)
            {
                return false;
            }
            else
            {
                u = D3Control.curTarget;
                D3Control.output("New Target ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                return true;
            }
        }

        protected override void DoExecute(D3Player Entity)
        {
            // return after 10 seconds regardless
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(5 * 1000, false);
            D3Unit originalTarget, u;

            originalTarget = D3Control.curTarget;
            if (!pickTarget())
            {
                // if we don't pick a target we must use the default provided else we will
                // get into a deadlock
                u = originalTarget;
                D3Control.output("DoEx Orig ID: " + u.ID + " HP: " + (int)u.Hp + " Dist: " + (int)u.DistanceFromPlayer + " ML: " + u.MLevel);
                D3Control.TargetManager.SetAttackTarget(originalTarget);

                if (u.IsElite && enableSaveFury)
                {
                    if ((D3Control.canCast("Wrath of the Berserker") && D3Control.isOnCD("Wrath of the Berserker")) ||
                        (D3Control.canCast("Earthquake") && D3Control.isOnCD("Earthquake")) ||
                        (D3Control.canCast("Call of the Ancients") && D3Control.isOnCD("Call of the Ancients")))
                    {
                        D3Control.output("ACTIVATE SAVE FURY");
                        saveFury = true;
                    }
                }

            }

            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(50);
				if (checkLootTimer.IsReady)
				{
					D3Control.checkLoots();
                }
				if (combatThresholdTimer.IsReady)
                   D3Control.output("Combat Timer Expired!");

                if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                    break;

                D3Control.updateDungoneInfo();

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
                        D3Control.output("No target found, returning from DoExecute.");
                        break;
                    }
                }
            }
        }

        public static void checkBuff(D3Player entity)
        {
            D3Unit target = D3Control.curTarget;

            if (D3Control.canCast("Battle Rage") && !D3Control.HasBuff("Battle Rage"))
            {
                CastBarbarianSpell("Battle Rage", D3Control.Player.Location);
				Thread.Sleep(50);
            }

            if (D3Control.canCast("War Cry") && !D3Control.HasBuff("War Cry"))
            {
                CastBarbarianSpell("War Cry", D3Control.Player.Location);
				Thread.Sleep(50);
            }

            if (D3Control.canCast("Threatening Shout") && (targetsInRange8 > 0) && D3Control.isOnCD("Threatening Shout"))
            {
                CastBarbarianSpell("Threatening Shout", D3Control.Player.Location);
            }

            if (D3Control.canCast("Ignore Pain") && (LastValidHpPct < ignorePainPct) && D3Control.isOnCD("Ignore Pain"))
            {
                CastBarbarianSpell("Ignore Pain", D3Control.Player.Location);
            }

            if (target.IsElite && !target.IsDead && (target.DistanceFromPlayer <= 25) && D3Control.canCast("Wrath of the Berserker") && D3Control.isOnCD("Wrath of the Berserker"))
            {
                CastBarbarianSpell("Wrath of the Berserker", D3Control.Player.Location);
				Thread.Sleep(50);
            }

            if (target.IsElite && !target.IsDead && isMeleeRange(target) && D3Control.canCast("Call of the Ancients") && D3Control.isOnCD("Call of the Ancients"))
            {
                CastBarbarianSpell("Call of the Ancients", D3Control.Player.Location);
            }
        }

        bool avoidAoE()
        {
            if (oldSafeSpot != safeSpot)
            {
                //D3Control.output("New Safe Spot X: " + safeSpot.X + " Y: " + safeSpot.Y + " Z: " + safeSpot.Z);
                if (moveBackTimer.IsReady)
                {
                    moveBackTimer.Reset();

                    D3Control.output("Try to avoid AoE!");

                    if (D3Control.canCast("Furious Charge") && D3Control.isOnCD("Furious Charge"))
                    {
                        CastBarbarianSpell("Furious Charge", safeSpot);
                        Thread.Sleep(200);
                        return true;
                    }

                    if (D3Control.canCast("LeapAttack") && D3Control.isOnCD("LeapAttack"))
                    {
                        CastBarbarianSpell("LeapAttack", safeSpot);
                        Thread.Sleep(100);
                        return true;
                    }

                    float distance = D3Control.Player.DistanceTo(safeSpot);
                    D3Control.ClickMoveTo(safeSpot);
                    Thread.Sleep(AoESleepTime);

                    return true;
                }

                oldSafeSpot = safeSpot;
            }

            return false;
        }
		
        void attackAoE()
        {
			// Set target
            D3Unit target = D3Control.curTarget;
			//
            if (D3Control.canCast("Ground Stomp") && ((targetsInRange10 > 0) || isMeleeRange(target)) && D3Control.isOnCD("Ground Stomp"))
            {
                CastBarbarianSpell("Ground Stomp", D3Control.Player.Location);
            }
            // spam revenge
            if (D3Control.canCast("Revenge") && ((targetsInRange15 > 0) || isMeleeRange(target)))
            { 
                CastBarbarianSpell("Revenge", D3Control.Player.Location);
            }
		
            if (D3Control.canCast("Overpower") && ((targetsInRange8 > 0) || isMeleeRange(target)) && D3Control.isOnCD("Overpower"))
            {
                CastBarbarianSpell("Overpower", D3Control.Player.Location);
            }

            if (D3Control.canCast("Rend") && rendTimer.IsReady && isMeleeRange(target))
            {
                rendTimer.Reset();
                CastBarbarianSpell("Rend", D3Control.Player.Location);
            }

            // wait until we are in melee range to use Earthquake
            if (D3Control.canCast("Earthquake") && target.IsElite && !target.IsDead && isMeleeRange(target) && D3Control.isOnCD("Earthquake"))
            {
                CastBarbarianSpell("Earthquake", D3Control.Player.Location);
            }
        }

        void dumpFury()
        {
            D3Unit target = D3Control.curTarget;
			// Hammer of the Ancients
            if (D3Control.canCast("Hammer of the Ancients") && isMeleeRange(target))
            {
                CastBarbarianDirectionSpell("Hammer of the Ancients", target.Location);
				return;
            }
			// Siesmic Slam
            if (D3Control.canCast("Siesmic Slam") && isMeleeRange(target))
            {
                CastBarbarianDirectionSpell("Siesmic Slam", target.Location);
				return;
            }
			// Weapon Throw
            if (D3Control.canCast("Weapon Throw") && ((int)target.DistanceFromPlayer <= 30))
            {
                CastBarbarianDirectionSpell("Weapon Throw", target.Location);
				return;
            }        
        }
		
        void generateFury()
        {
            D3Unit target = D3Control.curTarget;
			// If we are in Melee Range generate the fury needed
			if (isMeleeRange(target))
			{
				// Frenzy
				if (D3Control.canCast("Frenzy") && CastBarbarianSpell("Frenzy", target.Location))
					return;
				// Bash
				if (D3Control.canCast("Bash") && CastBarbarianSpell("Bash", target.Location))
					return;
				// Cleave
				if (D3Control.canCast("Cleave") && CastBarbarianSpell("Cleave", target.Location))
					return;
			}
			return;
        }

        /*  
           Order of operatations:
           1. Sanity check
           2. Buffs and debuffs
           3. AoE
           4. If needed get nearby health globe or use health pot
           5. If needed move out of bad stuff on ground
           6. Gap closers (leap, charge, spear)
           7. Fury dumps (weapon throw, siesmic slam, rend, hammer of ancients)
           7. Melee if in range
           8. Walk to be in range
        */
        void attackTarget(D3Player entity)
        {		
            D3Unit target = D3Control.curTarget;
            Vector3D leapTarget;
            int CurHpPct;
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
			//
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
			// Update HP
            CurHpPct = D3Control.Player.HpPct;
            if ((CurHpPct > 0) && (CurHpPct <= 100))
                LastValidHpPct = CurHpPct;
			// Avoid AOE if we need to and are set to.
            if (AvoidAoE && avoidAoE())
                return;
            // if we are low life after AoE try to pot
            if (LastValidHpPct <= HealthPotHpPct)
            {
                    // D3Control.output("Try to use health potion! LastValidHpPct: " + LastValidHpPct);
                    D3Control.usePotion();
            }
            // War Cry, Battle Rage, Threatening Shout, Ignore Pain
            checkBuff(entity);
			// Health Globe
            if ((LastValidHpPct <= HealthPotHpPct) && moveForHealthGlobe)
            {
                D3Object healthGlobe = D3Control.ClosestHealthGlobe;
                if (healthGlobe != null)
                {
                    D3Control.MoveTo(healthGlobe, 2.5f);
                    return;
                }
            }
			// Check if door is in the way.
            if (D3Control.getDoorInWay(target) != null)
            {
                D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(target, 2.5f);
                return;
            }
			// Whirlwind
			var enemies = D3Control.TargetManager.GetAroundEnemy(WhirlwindRange).Count;
			if (D3Control.canCast("Whirlwind") && D3Control.Player.Fury > WhirlWindFuryAmount && enemies >= NumofEnemiesInWhirlwindRange)
			{
                Vector3D tLoc = target.Location;
                Vector3D pLoc = D3Control.Player.Location;
                float angle = (float)Math.Atan2(tLoc.Y - pLoc.Y, tLoc.X - pLoc.X);
                if (angle < 0)
                    angle = (float)(angle + Math.PI * 2);
                Vector3D location = D3Control.getTargetSideSpot(tLoc, angle, -20);
				if (CastBarbarianSpell("Whirlwind", location))
				{
					return;
				}
			}
            // Revenge, Overpower, Ground Stomp, Earthquake
            attackAoE();
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
			// Leap Attack
            if (D3Control.canCast("LeapAttack") && D3Control.isOnCD("LeapAttack"))
            {
                leapTarget = target.Location;

                if ((LastValidHpPct <= HealthGlobeHpPct) && useLeapHealthGlobe)
                {
                    D3Object healthGlobe = D3Control.ClosestHealthGlobe;
                    if (healthGlobe != null)
                    {
                        // adjust the landing point so we land just past it
                        // this should pick it up on landing
                        leapTarget = healthGlobe.Location;

                        if (D3Control.Player.Location.X <= leapTarget.X) { 
                            leapTarget.X += 1;
                        } else {
                            leapTarget.X -= 1;
                        }

                        if (D3Control.Player.Location.Y <= leapTarget.Y) { 
                            leapTarget.Y += 1;
                        } else {
                            leapTarget.Y -= 1;
                        }
                  
                        D3Control.output("Leap to health globe! Player X: " + D3Control.Player.Location.X + " Y: " + D3Control.Player.Location.Y);      
                        D3Control.output("Leap to health globe! Target X: " + leapTarget.X + " Y: " + leapTarget.Y);
                    }
                }
				// Cast
                CastBarbarianSpell("LeapAttack", leapTarget);
                return;
            }
			// Furious Charge
            if (D3Control.canCast("Furious Charge") && D3Control.isOnCD("Furious Charge"))
            {
                CastBarbarianSpell("Furious Charge", target.Location);
                return;
            }
			// Ancient Spear
            if (D3Control.canCast("Ancient Spear") && D3Control.isOnCD("Ancient Spear"))
            {
                CastBarbarianSpell("Ancient Spear", target.Location);
                return;
            }
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
			// Dump Fury
            dumpFury();
			// Generate Fury
			generateFury();
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
            if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward && !target.IsDead)
            {
				// Whirlwind
				if (D3Control.canCast("Whirlwind") && D3Control.Player.Fury > WhirlWindFuryAmount)
				{
					Vector3D tLoc = target.Location;
					Vector3D pLoc = D3Control.Player.Location;
					float angle = (float)Math.Atan2(tLoc.Y - pLoc.Y, tLoc.X - pLoc.X);
					if (angle < 0)
						angle = (float)(angle + Math.PI * 2);
					Vector3D location = D3Control.getTargetSideSpot(tLoc, angle, -10);
					if (CastBarbarianSpell("Whirlwind", location))
					{
						return;
					}
				}
				// Rend
				if (D3Control.canCast("Rend"))
				{
					rendTimer.Reset();
					CastBarbarianSpell("Rend", D3Control.Player.Location);
				}
				// Frenzy
                if (D3Control.canCast("Frenzy") && CastBarbarianSpell("Frenzy", target.Location))
                    return;
				// Bash
                if (D3Control.canCast("Bash") && CastBarbarianSpell("Bash", target.Location))
                    return;
				// Cleave
                if(D3Control.canCast("Cleave") && CastBarbarianSpell("Cleave", target.Location))
                    return;
            }
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
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
        }

        protected override void DoExit(D3Player entity)
        {
            D3Control.output("DoExit");
            //on exit, if there is a previous state, go back to it
            if (PreviousState != null)
            {
                CallChangeStateEvent(entity, PreviousState, false, false);
            }
        }

        protected override void DoFinish(D3Player entity)
        {
            D3Control.output("DoFinish");
        }
    }
}