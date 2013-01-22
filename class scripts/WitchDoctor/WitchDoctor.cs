/*
	Witch Doctor Class Script by ASWeiler
    Last Edited: 12/14/2012
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

namespace Astronaut.Scripts.WitchDoctor
{
    public class Core : GlobalBaseBotState
    {
        public static int RestHp = 40;
		// Start DoEnter! (The main function)
        protected override void DoEnter(D3Player Entity)
        {
			// Set the base of DoEnter based off class
            base.DoEnter(Entity);
            // Init Message
            D3Control.output("Class Script by ASWeiler Loaded");
            // We override the default states with our own
            combatState = new WitchDoctor.CombatState();
        }
		//
        protected override bool IsHealer()
        {
            return false;
        }
		//
        public static void Emergency(D3Player player)
        {
			D3Control.output("EMERGENCY");
        }
		//
        protected void CastInterruption()
        {
			D3Control.output("CAST INERRUPTION");
        }
		//
        public static void DebuffAll()
        {
			D3Control.output("DEBUFF ALL");
        }

        public override bool NeedRest(D3Player player)
        {
            return false;
        }
    }
    public class CombatState : Common.CombatState
    {
		/*
			START Class Script Options
		*/
		// Use On HP % OPTIONS
		static int hpPct_UsePotion = 80; 										// The HP % to use a potion on
		static int hpPct_HealthGlobe = 90;										// The HP % to search for a health globe.
		// Zombie Charger OPTIONS
		static int ZombieChargerMana = 40;										// The amount of mana you need to have before casting Zombie Charger
		// Spirit Walk Settings	
		static bool movebackonSpirtWalk = false;								// Should we run back when we go in to Spirit Walk?
		public int hpPct_SpiritWalk = 100;	  									// The HP % to search for a health globe. 100 = cast at every chance possible
		static bool SpiritWalktoHealthGlobe = true;								// Use Spirit Walk to get to Health Globe faster
		// Acid Cloud OPTIONS
		public int ManatocastAcidCloud = 300;								// Set the amount of Mana needed to use Acid Cloud
		static CWSpellTimer acidCloudTimer = new CWSpellTimer(1 * 1000);	// The amount of time we should wait before casting Acid Cloud again (2 * 1000 = 2 seconds)
		// Check for loot Options
		static CWSpellTimer checkLootTimer = new CWSpellTimer(1 * 1000, false);	// Check for loot
		// Avoiding AOE OPTIONS
		static bool AvoidAoE = true;            								//  True = try to avoid AoEs that are set to be avoided in the bot's App Options -> Tools -> Combat tab
		static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1000);		// Checking for safe spot timer (default 1 sec)
		static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000); 		// Move back timer in seconds
		// Spell Timers
		// --> Change these if you want them to cast more often. ---> 2 * 1000 = 2 seconds
        static CWSpellTimer batTimer = new CWSpellTimer(2500);
        static CWSpellTimer hauntTimer = new CWSpellTimer(2 * 1000);
        static CWSpellTimer locustTimer = new CWSpellTimer(6 * 1000);
        // DISABLED FOR THE OPTION ABOVE   static CWSpellTimer ZombieChargerTimer = new CWSpellTimer(1000);
		/*
			END Class Script Options
		*/
		
		
		// DO NOT CHANGE THESE
		static Vector3D safeSpot = null, oldSafeSpot = null;
		static bool isVisionQuest = false;
        public static bool inited = false;
        const int skillInterval = 300;
        const int ZombieDogsID = 0x00019331;

        protected override void DoEnter(D3Player Entity)
        {
			//D3Control.output("Do Enter");
        }
		// Cast Spell on Target
        public static bool CastWitchDoctorTargetSpell(string SpellName, D3Object target, out bool castSuccessfully)
        {
            castSuccessfully = false;
            if (hasEnoughResourceForSpell(SpellName))
            {
                return D3Control.NewCastTargetSpell(SpellName, target, out castSuccessfully);
                
            }
            return false;
        }
		// Cast Spell on Location
        public static bool CastWitchDoctorSpell(string SpellID, Vector3D loc)
        {
            if (hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastLocationSpell(SpellID, loc, true);
            }
            return false;
        }
		// Make sure we have the mana needed to cast
        public static bool hasEnoughResourceForSpell(string SpellID)
        {
            return D3Control.WDHasEnoughManaForSpell(SpellID);
        }

        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        protected override void DoExecute(D3Player Entity)
        {
            isVisionQuest = D3Control.isPassiveSkillLoaded("Vision Quest");

            if (D3Control.curTarget != null)
            {
                CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);// in case something is wrong
                bool enemyFound = false;
                while (true)
                {
                    Thread.Sleep(50);   // sleep a bit to lower cpu usage
					//
                    if (!D3Control.IsInGame() || D3Control.Player.IsDead || combatThresholdTimer.IsReady)
                        break;
					// Check if we should look for AOE to avoid and avoid it if we are set to and we are in combat.
					if (AvoidAoE && checkSafeSpotTimer.IsReady && D3Control.Player.isInCombat)
					{
						// Reset safe spot checking timer
						checkSafeSpotTimer.Reset();
						// Get AOE info
						var dTargetInfo = D3Control.getDangourousTargetInfo();
						// Set the safe spot to an known safe spot based off the AOE info
						safeSpot = D3Control.getSafePoint(dTargetInfo);
						// If the oldSafeSpot is not the same as the current SafeSpot then get out of the AOE
						if (oldSafeSpot != safeSpot)
						{
							// If the moveBackTimer is ready to use again, avoid the AOE
							if (moveBackTimer.IsReady)
							{
								// Reset the timer
								moveBackTimer.Reset();
								// Avoid the AOE by clicking to the safe spot to move out of the AOE
								D3Control.output("Try to avoid AoE!");
								D3Control.ClickMoveTo(safeSpot);
								Thread.Sleep(750);
							}
							// Reset
							oldSafeSpot = safeSpot;
						}
					}
					// Spirit Walk
					if (hpPct_SpiritWalk > 99 || D3Control.Player.HpPct < hpPct_SpiritWalk)
					{
						if (CastWitchDoctorSpell("Spirit Walk", D3Control.Player.Location))
						{
							// Run back 20 yards
							if (moveBackTimer.IsReady && movebackonSpirtWalk)
							{
								if (D3Control.curTarget.DistanceFromPlayer < 60)
								{
									moveBackTimer.Reset();
									// Click Move Backwards
									D3Control.ClickMoveTo(D3Control.getSideSpot(D3Control.Player.Location, 180, 20));
									while (D3Control.Player.isMovingForward)
									{
										Thread.Sleep(10);
									}
								}
							}
							Thread.Sleep(50);
						}
					}
					// Health Globe
					if (D3Control.Player.HpPct < hpPct_HealthGlobe)
					{
						D3Object healthGlobe = D3Control.ClosestHealthGlobe;
						if (healthGlobe != null)
						{
							if (SpiritWalktoHealthGlobe)
								CastWitchDoctorSpell("Spirit Walk", D3Control.Player.Location);
							D3Control.MoveTo(healthGlobe, 2.5f);
						}
					}
					// Check if we need to use a potion first
					if (D3Control.Player.HpPct < hpPct_UsePotion)
					{
						// Since we do, use the potion
						D3Control.usePotion();
					}
					// Check if we need to cast buffs
					checkBuff(Entity);
					//
                    D3Control.checkLoots();
					//
                    if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                    {
                        // make sure this function call is added if you have a while loop in DoExecute to handle the target selection.
                        // it handles some boss fights, where you have to kill the minions first.
                        D3Control.TargetManager.handleSpecialFight();
                        doPulling(D3Control.Player);
                        continue;
                    }
                    else
                    {
                        D3Control.TargetManager.ClearTarget();
                        var enemies = D3Control.TargetManager.GetAroundEnemy(65);
                        enemyFound = false;
                        foreach (D3Unit enemy in enemies)
                        {
                            if (!D3Control.LOS(enemy.Location))
                            {
                                D3Control.TargetManager.SetAttackTarget(enemy);
                                break;
                            }
                        }

                        if (!D3Control.isObjectValid(D3Control.curTarget))
                        {
                            return;
                        }
                        enemyFound = true;
                    }
                    if (enemyFound)
                        continue;
                    if (!D3Control.Player.isInCombat || combatThresholdTimer.IsReady)
                        break;
                }
                return;

            }
            // Check if we already have a valid Unit to attack from a previous state
            else if (D3Control.curTarget == null)
            {
                D3Control.output("Looking for a new enemy to attack");
                return;
            }
		}
		// Handle Elites with special spells
        void handleElite()
        {
            try
            {
                if (CastWitchDoctorSpell("Big Bad Voodoo", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                }
                if (CastWitchDoctorSpell("Fetish Army", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                }
                if (CastWitchDoctorSpell("Spirit Barrage", D3Control.Player.Location))
                {
                    D3Control.Player.Wait(skillInterval);
                }
            }
            catch { }
        }
		// Move Closer
        void moveCloser(D3Object target)
        {
            if (target.DistanceFromPlayer > 5)
                D3Control.MoveTo(target, target.DistanceFromPlayer - 5);
        }
        void doSkillCastVisionQuest()
        {
            if (isVisionQuest && D3Control.getCDSkillNumber() < 4)
            {
                if (CastWitchDoctorSpell("Fetish Army", D3Control.Player.Location))
                    Thread.Sleep(10);
                if (CastWitchDoctorSpell("Wall of Zombies", D3Control.Player.Location))
                    Thread.Sleep(10);
                if (CastWitchDoctorSpell("Big Bad Voodoo", D3Control.Player.Location))
                    Thread.Sleep(10);
                if (CastWitchDoctorSpell("Mass Confusion", D3Control.Player.Location))
                    Thread.Sleep(10);
                if (CastWitchDoctorSpell("Hex", D3Control.Player.Location))
                    Thread.Sleep(10);
                if (CastWitchDoctorSpell("Spirit Walk", D3Control.Player.Location))
                    Thread.Sleep(10);
            }
        }
        void HandleAOE()
        {
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
			//
			bool castSuccessfully = false;
			// 40ft AOE Spells
			var mobswithin40 = D3Control.TargetManager.GetAroundEnemy(40).Count;
			if (mobswithin40 > 0)
			{
				// Zombie Charger
				if (D3Control.canCast("Zombie Charger") && D3Control.Player.Mp >= ZombieChargerMana)
				{
					// Reset the timer
					ZombieChargerTimer.Reset();
					// Stop moving
					if (!D3Control.isMovingWorking())
					{
						D3Control.stopMoving();
					}
					// Cast Zombie Charger
					CastWitchDoctorTargetSpell("Zombie Charger", D3Control.curTarget, out castSuccessfully);
                    if (!castSuccessfully)
                    {   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(D3Control.curTarget);
                    }
				}
			}
			// Summon + Boom!
			if (D3Control.canCast("Summon Zombie Dogs") && D3Control.canCast("Sacrifice") && D3Control.Player.Mp < 200)
			{
				D3Control.output("SACRIFICE THE DOGS");
				CastWitchDoctorSpell("Summon Zombie Dogs", D3Control.Player.Location);
				CastWitchDoctorSpell("Sacrifice", D3Control.Player.Location);
				D3Control.Player.Wait(skillInterval);
			}
			// 12ft AOE Spells
			var mobswithin12 = D3Control.TargetManager.GetAroundEnemy(12).Count;
			if (mobswithin12 > 4)
			{
				// Horrify
				if (CastWitchDoctorSpell("Horrify", D3Control.Player.Location))
				{
					D3Control.Player.Wait(skillInterval);
				}
				// Hex
				if (CastWitchDoctorSpell("Hex", D3Control.Player.Location))
				{
					D3Control.Player.Wait(skillInterval);
				}
			}
			// 16ft AOE Spells
			var mobswithin16 = D3Control.TargetManager.GetAroundEnemy(16).Count;
			if (mobswithin16 > 2)
			{
				// Soul Harvest
				if (CastWitchDoctorSpell("Soul Harvest", D3Control.Player.Location))
				{
					D3Control.Player.Wait(skillInterval);
				}
				// Mass Confusion
				if (CastWitchDoctorSpell("Mass Confusion", D3Control.Player.Location))
				{
					D3Control.Player.Wait(skillInterval);
				}
				// Wall of Zombies
				if (D3Control.canCast("Wall of Zombies"))
				{
					CastWitchDoctorTargetSpell("Wall of Zombies", D3Control.curTarget, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(D3Control.curTarget);
					}
				}
			}
			// Acid Cloud
			if ((acidCloudTimer.IsReady || D3Control.Player.Mp > ManatocastAcidCloud) && D3Control.canCast("Acid Cloud"))
			{
				acidCloudTimer.Reset();
				// Acid Cloud
				CastWitchDoctorTargetSpell("Acid Cloud", D3Control.curTarget, out castSuccessfully);
                if (!castSuccessfully)
                {   // cannot cast the spell, such as obstacle, untargetable?
					moveCloser(D3Control.curTarget);
                }
			}
        }
        public bool moveToStartFight(D3Player entity)
        {
            HandleAOE();
			//
            if (D3Control.getDoorInWay(D3Control.curTarget) != null)
            {
                D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(D3Control.curTarget, 2.5f);      // a thread doing the move
                return true;
            }

            if (D3Control.curTarget == null)
                return false;

            float d = D3Control.curTarget.DistanceFromPlayer;
            if (d > 38)
            {
                if (D3Control.LOS(D3Control.curTarget.Location))
                    D3Control.MoveTo(D3Control.curTarget, 25);      // a thread doing the move
                else
                    D3Control.MoveTo(D3Control.curTarget, 38);      // a thread doing the move
            }
            else
            {
            }
            if (d < 7.5)
                return true;
            if (D3Control.isObjectValid(D3Control.curTarget) && D3Control.LOS(D3Control.curTarget.Location))
            {
                if (!D3Control.isMovingWorking())
                {   // for casters, if LOS(line of sight)
                    if (d <= 5)
                    {
                        D3Control.MoveTo(D3Control.curTarget, 2.5f);      // a thread doing the move
                        return true;
                    }
                    else
                        D3Control.MoveTo(D3Control.curTarget, d - 5f);      // a thread doing the move
                }
                return false;
            }
            return true;
        }
        public static void checkBuff(D3Player entity)
        {// check buff and aura
            try
            {
                if (D3Control.canCast("Summon Zombie Dogs") && (D3Control.GetPetCount(ZombieDogsID) < 3 || isVisionQuest))
                {
                    if (CastWitchDoctorSpell("Summon Zombie Dogs", D3Control.Player.Location))
                        Thread.Sleep(skillInterval);
                }
                if (D3.D3Control.canCast("Gargantuan") && (!D3Control.HasWDGagantuan() || isVisionQuest))
                {
                    CastWitchDoctorSpell("Gargantuan", D3Control.Player.Location);
                    Thread.Sleep(skillInterval);

                }
            }
            catch { }
            return;
        }
		// Do Pulling
        void doPulling(D3Player entity)
        {
			Thread.Sleep(50);
			//
			bool castSuccessfully = false;
			// Set the target variable to the current target set by Target Manager
            D3Unit target = D3Control.curTarget;
			// Check if we should look for AOE to avoid and avoid it if we are set to and we are in combat.
			if (AvoidAoE && checkSafeSpotTimer.IsReady && D3Control.Player.isInCombat)
			{
				// Reset safe spot checking timer
				checkSafeSpotTimer.Reset();
				// Get AOE info
				var dTargetInfo = D3Control.getDangourousTargetInfo();
				// Set the safe spot to an known safe spot based off the AOE info
				safeSpot = D3Control.getSafePoint(dTargetInfo);
				// If the oldSafeSpot is not the same as the current SafeSpot then get out of the AOE
				if (oldSafeSpot != safeSpot)
				{
					// If the moveBackTimer is ready to use again, avoid the AOE
					if (moveBackTimer.IsReady)
					{
						// Reset the timer
						moveBackTimer.Reset();
						// Avoid the AOE by clicking to the safe spot to move out of the AOE
						D3Control.output("Try to avoid AoE!!!");
						D3Control.ClickMoveTo(safeSpot);
						Thread.Sleep(750);
					}
					// Reset
					oldSafeSpot = safeSpot;
					return;
				}
			}
			// Spirit Walk
			if (hpPct_SpiritWalk > 99 || D3Control.Player.HpPct < hpPct_SpiritWalk)
			{
				if (CastWitchDoctorSpell("Spirit Walk", D3Control.Player.Location))
				{
					// Run back 20 yards
					if (moveBackTimer.IsReady && movebackonSpirtWalk)
					{
						if (D3Control.curTarget.DistanceFromPlayer < 60)
						{
							moveBackTimer.Reset();
							// Click Move Backwards
							D3Control.ClickMoveTo(D3Control.getSideSpot(D3Control.Player.Location, 180, 20));
							while (D3Control.Player.isMovingForward)
							{
								Thread.Sleep(10);
							}
							return;
						}
					}
					Thread.Sleep(50);
				}
			}
            if (D3Control.Player.HpPct < hpPct_HealthGlobe)
            {
                D3Object healthGlobe = D3Control.ClosestHealthGlobe;
                if (healthGlobe != null)
                {
					if (SpiritWalktoHealthGlobe)
						CastWitchDoctorSpell("Spirit Walk", D3Control.Player.Location);
                    D3Control.MoveTo(healthGlobe, 2.5f);
                    return;
                }
            }
			// Check if we need to use a potion first
			if (D3Control.Player.HpPct < hpPct_UsePotion)
			{
				// Since we do, use the potion
				D3Control.usePotion();
			}
			// Check if we need to cast buffs
            checkBuff(entity);
			// Check for loot
			if (checkLootTimer.IsReady)
			{
				D3Control.checkLoots();
				checkLootTimer.Reset();
			}
			// Check before pull
            if (!GlobalBaseBotState.checkBeforePull(entity))
                return;
			//
            if (!D3Control.isObjectValid(target))
                return;
            if (!moveToStartFight(entity))
                return;
            {
				// Handle Elites
				if (target.IsElite)
					handleElite();
				// Vision Quest
                doSkillCastVisionQuest();
				// Make sure the target is still valid and alive
				if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
				}
				/*
					Target Based Spells
				*/
				// Spirit Barrage
				if (D3Control.canCast("Spirit Barrage"))
				{
					CastWitchDoctorTargetSpell("Spirit Barrage", target, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
					}
				}
				// Make sure the target is still valid and alive
				if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
				}
				// Grasp of the Dead
				if (D3Control.canCast("Grasp of the Dead"))
				{
					CastWitchDoctorTargetSpell("Grasp of the Dead", target, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
					}
				}
				// Fire Bats
				if (batTimer.IsReady || isVisionQuest && D3.D3Control.getCDSkillNumber() >= 4)
				{
					if (D3Control.canCast("Firebats"))
					{
						CastWitchDoctorTargetSpell("Firebats", target, out castSuccessfully);
						if (!castSuccessfully)
						{   // cannot cast the spell, such as obstacle, untargetable?
							moveCloser(target);
						}
					}
				}
				// Haunt
				if (hauntTimer.IsReady)
				{
					if (D3Control.canCast("Haunt"))
					{
						CastWitchDoctorTargetSpell("Haunt", target, out castSuccessfully);
						if (!castSuccessfully)
						{   // cannot cast the spell, such as obstacle, untargetable?
							moveCloser(target);
						}
					}
				}
				// Locust Swarm
				if ((locustTimer.IsReady || isVisionQuest && D3Control.getCDSkillNumber() >= 4) && target.DistanceFromPlayer < 30)
				{
					if (D3Control.canCast("Locust Swarm"))
					{
						CastWitchDoctorTargetSpell("Locust Swarm", target, out castSuccessfully);
						if (!castSuccessfully)
						{   // cannot cast the spell, such as obstacle, untargetable?
							moveCloser(target);
						}
					}
				}
				// Make sure the target is still valid and alive
				if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
				}
				// Corpse Spiders
				if (D3Control.canCast("Corpse Spiders"))
				{
					CastWitchDoctorTargetSpell("Corpse Spiders", target, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
					}
				}
				// Plauge of Toads
				if (D3Control.canCast("Plague of Toads"))
				{
					CastWitchDoctorTargetSpell("Plague of Toads", target, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
					}
				}
				// Poison Dart
				if (D3Control.canCast("Poison Dart"))
				{
					CastWitchDoctorTargetSpell("Poison Dart", target, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
					}
				}
				// Fire Bomb
				if (D3Control.canCast("Firebomb"))
				{
					CastWitchDoctorTargetSpell("Firebomb", target, out castSuccessfully);
					if (!castSuccessfully)
					{   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
					}
				}
				// Make sure the target is still valid and alive
				if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
				{
					D3Control.output("The target does not exist anymore, looking for another now.");
					return;
				}
            }

            if (!D3Control.isMovingWorking())
            {
                D3Control.stopMoving();
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