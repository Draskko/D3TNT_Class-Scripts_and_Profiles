/*
	Wizard Class Script by ASWeiler
    Last Edited: 11/22/2012
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
		static int ouputMode = 1;					// 0 = Minimal Output | 1 = Normal Output | 2 = Debug Output
		// Focus target OPTIONS
		static bool FocusPackLeader = false;     	// True = focus target elite pack leader until dead
		static bool FocusTreasureGoblin = true;		// True = focus attacking Treasure Goblin until dead
		static bool FocusMobSummoner = true;		// True = focus attacking any mob summoners until dead
		// Scan and Attack Distance OPTIONS
        static int  MobScanDistance = 50;			// Scan radius for mobs (maximum 100 or about two screens)
		const int attackDistance = 10; 				// Set how far back we attack from
		// Avoiding AOE OPTIONS
		static bool AvoidAoE = true;            	// try to avoid AoE (desecrate and middle of arcane beams)
		// Archon Mode OPTIONS
		const int arcaneStrikeRange = 10;			// The range in which the target must be within before casting Arcane Strike (50 = One Screen Length)
		const int arcaneBlastRange = 10;			// The range in which the target must be within before casting Arcane Blast (50 = One Screen Length)
		const int minDisintegrationWaveRange = 0;	// The minimum range in which the target can be within while still being able to cast Disintegration Wave (0 = The target can be right on top of the bot and it will still cast it)
		const int maxDisintegrationWaveRange = 50;	// The maximum range in which the target can be within while still being able to cast Disintegration Wave (50 = One Screen Length)
		// Use on Arcane Power amount OPTIONS
		const int ArcanePower_EnergyTwister = 40;	// The amount of Arcane Power required to have before casting Energy Twister
		const int ArcanePower_ExplosiveBlast = 40;	// The amount of Arcane Power required to have before casting Explosive Blast
		const int ArcanePower_Blizzard = 40;		// The amount of Arcane Power required to have before casting Blizzard
		const int ArcanePower_Meteor = 40;			// The amount of Arcane Power required to have before casting Meteor
		// Use On HP % OPTIONS
		static int hpPct_UsePotion = 80; 			// The HP % to use a potion on
		static int hpPct_DiamondSkin = 100;			// The HP % to cast Diamond Skin
		static int hpPct_HealthGlobe = 90;			// The HP % to search for a health globe.
		// Power Hungry
		static bool isPowerHungry = false;			// True = You are using the Power Hungry passive spell and want to grab HP Globe to get Arcane Power
		static int PowerHungry_HealthGlobe = 40;	// If you use Power Hungry passive skill, set the Arcane Power level you want to search for a health globe.
        /* END CONFIGURATION OPTIONS */
		
		/* SPELL TIMERS */
        static CWSpellTimer moveBackTimer = new CWSpellTimer(3 * 1000); 		// Move back timer in seconds
		static CWSpellTimer checkBuffTimer = new CWSpellTimer(1 * 1000, false);	// Buff Check timer
        static CWSpellTimer checkSafeSpotTimer = new CWSpellTimer(1000);		// Checking for safe spot timer (default 1 sec)
		static CWSpellTimer teleportTimer = new CWSpellTimer(1 * 1000);			// Teleport Timer
        static CWSpellTimer meteorTimer = new CWSpellTimer(1 * 1000);			// Meteor Timer
        static CWSpellTimer blizzardTimer = new CWSpellTimer(500);				// Blizzard Timer (500 = 1/2 an second)
        static CWSpellTimer familiarTimer = new CWSpellTimer(5 * 60 * 1000);	// Familiar Timer
        static CWSpellTimer MagicWeaponTimer = new CWSpellTimer(5 * 60 * 1000);	// Magic Weapon Timer
		static CWSpellTimer lootcheckTimer = new CWSpellTimer(1 * 1000);		// Check for loot Timer
		static CWSpellTimer checkInGameTimer = new CWSpellTimer(1 * 1000);		// Check in game Timer
		/* END SPELL TIMERS */
		
		/* MISC VARIABLES (DO NOT CHANGE UNLESS YOU KNOW WHAT YOU ARE DOING) */
		const int skillInterval = 50; 													// Set how long we should wait after casting a spell
        const int HydraNumber = 1;														// Hydra
        // Escape AOE Thread
		static Thread keepSpellThread;
        static Vector3D safeSpot = null, oldSafeSpot = null;
		static Dictionary<string, int> abilityInfoDic = new Dictionary<string, int>();	// Set the variable for our Spells Dictionary
		/* END MISC VARIABLES */
		
		
		/* FUNCTIONS */
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
		
		// Keep Spell Function
        public static void keepSpell()
        {
            Random rgen = new Random();
            int seed = rgen.Next();
			if (ouputMode > 1)
				D3Control.output("Starting Spell Thread. Seed: " + seed);
            try
            {
                while (true)
                {
					// If we are not in game, skip the rest
					if (checkInGameTimer.IsReady)
					{
						if (!D3Control.IsInGame())
						{
							if (ouputMode > 1)
								D3Control.output("Not in game anymore. Aborting Spell Thread! Seed: " + seed);             
							return;
						}
						checkInGameTimer.Reset();
					}
					// Explosive Blast
					if (D3Control.canCast("Explosive Blast") && D3Control.Player.ArcanePower >= 40 && D3Control.Player.isMovingForward) //explosive blast on 6sec cd to open doors
					{
						CastWizardSpell("Explosive Blast", D3Control.Player.Location);
					}
					// Check for loot outside combat.
					if (lootcheckTimer.IsReady)
					{
						D3Control.checkLoots();
						lootcheckTimer.Reset();
					}
                    Thread.Sleep(500);
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
				// Init Message
				D3Control.output("Melee Wizard by ASWeiler Loaded");
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
                abilityInfoDic.Add("Archon", 25);
				abilityInfoDic.Add("Disintegration Wave", 0);
                abilityInfoDic.Add("Mirror Image", 0);
            }
        }
		
		// Function to try to cast a Wizard Spell using SpellID and location to cast spell
        public static bool CastWizardSpell(string SpellID, Vector3D loc)
        {
            if ((SpellID == "Spectral Blade" || SpellID == "Disintegration Wave" || SpellID == "Arcane Orb" || SpellID == "Teleport") && hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastDirectionSpell(SpellID, loc);
            }
            else if (hasEnoughResourceForSpell(SpellID))
            {
                return D3Control.CastLocationSpell(SpellID, loc, true);
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
			//				   3526   is Butcher
            if (((int)target.DistanceFromPlayer <= 10) || 
				((target.ID == 3526) && ((int)target.DistanceFromPlayer <= 10)) || 
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
		
        /// <summary>
        /// This happens when we are being attacked by some mobs or when we
        /// have found something to kill 
        /// </summary>
        protected override void DoExecute(D3Player Entity)
        {
            // return after 10 seconds regardless
            CWSpellTimer combatThresholdTimer = new CWSpellTimer(10 * 1000, false);
			
			bool enemyFound = false;
			
            // loop until we run out of targets or the timer expires
            while (true)
            {
				Thread.Sleep(50);
				// If the combat timer expires, break out of DoExecute
                if (combatThresholdTimer.IsReady)
				{
					if (ouputMode > 0)
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
				// Grab a health globe if we need to and one exists
				if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
				{
					D3Object healthGlobe = D3Control.ClosestHealthGlobe;
					if (healthGlobe != null)
					{
						do 
						{
							// Move to the Health Globe
							D3Control.MoveTo(D3Control.ClosestHealthGlobe, 2.5f);
							// Check if we need to use a potion
							if (D3Control.Player.HpPct < hpPct_UsePotion)
								D3Control.usePotion();
						} while (D3Control.Player.HpPct <= hpPct_HealthGlobe && !D3Control.Player.IsDead && D3Control.ClosestHealthGlobe != null);
						return;
					}
				}
				// Check if we need to use a potion
				if (D3Control.Player.HpPct < hpPct_UsePotion)
					D3Control.usePotion();
				// Explosive Blast
				if (D3Control.canCast("Explosive Blast") && D3Control.Player.ArcanePower >= ArcanePower_ExplosiveBlast)
				{
					CastWizardSpell("Explosive Blast", D3Control.Player.Location);
				}
				// Update dungeon info
				D3Control.updateDungoneInfo();
				//
                if (D3Control.isObjectValid(D3Control.curTarget) && !D3Control.curTarget.IsDead)
                {
                    // make sure this function call is added if you have a while loop in DoExecute to handle the target selection.
                    // it handles some boss fights, where you have to kill the minions first.
                    D3Control.TargetManager.handleSpecialFight();
                    MoveToandAttackTarget(D3Control.Player);
                    continue;
                }
                else
                {
                    D3Control.TargetManager.ClearTarget();
                    var enemies = D3Control.TargetManager.GetAroundEnemy(MobScanDistance);
                    enemyFound = false;
                    foreach (D3Unit enemy in enemies)
                    {
						// Focus Treasure Seeker
						if ((enemy.ID == 5985 || enemy.ID == 5984 || enemy.ID == 5985 || enemy.ID == 5987 || enemy.ID == 5988) && FocusTreasureGoblin)
						{
							if (ouputMode > 0)
							{
								D3Control.output("Found Treasure Goblin (ID: " + enemy.ID + " Dist: " + (int)enemy.DistanceFromPlayer+")");
							}
							D3Control.TargetManager.SetAttackTarget(enemy);
							break;				
						}
						// Focus Mob Summoner
						if ((enemy.ID == 5388 || enemy.ID == 5387 || enemy.ID == 4100) && FocusMobSummoner)
						{
							if (ouputMode > 0)
							{
								D3Control.output("Found Mob Summoner ID: "+enemy.ID+" Dist: "+(int)enemy.DistanceFromPlayer+")");
							}
							D3Control.TargetManager.SetAttackTarget(enemy);
							break;
						}
						// Focus Pack Leader
						if ((enemy.MLevel == 2) && FocusPackLeader)
						{
							if (ouputMode > 0)
							{
								D3Control.output("Found Pack Leader ID: "+enemy.ID+" Dist: "+(int)enemy.DistanceFromPlayer+")");
							}
							D3Control.TargetManager.SetAttackTarget(enemy);
							break;
						}
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
		}
		// Start the attack!
        void MoveToandAttackTarget(D3Player entity)
        {
			Thread.Sleep(50);
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
			//
            if (!GlobalBaseBotState.checkBeforePull(entity))
				return;
			// If we detect a door, deal with it.
            if (D3Control.getDoorInWay(target) != null)
            {
				// Move closer to the door
				if (ouputMode > 0)
					D3Control.output("A door inbetween? Try to move closer and open the door.");
                D3Control.MoveTo(target, 2.5f);      // a thread doing the move
                return;
            }
			// Diamond Armor Normal
			if ((D3Control.Player.HpPct < hpPct_DiamondSkin || hpPct_DiamondSkin > 99 ) && D3Control.canCast("Diamond Skin"))
			{
				D3Control.CastLocationSpell("Diamond Skin", D3Control.Player.Location, true);
			}
			// Grab a health globe if we need to and one exists
			if (D3Control.Player.HpPct <= hpPct_HealthGlobe)
			{
				D3Object healthGlobe = D3Control.ClosestHealthGlobe;
				if (healthGlobe != null)
				{
					do 
					{
						// Move to the Health Globe
						D3Control.MoveTo(D3Control.ClosestHealthGlobe, 2.5f);
						// Check if we need to use a potion
						if (D3Control.Player.HpPct < hpPct_UsePotion)
							D3Control.usePotion();
					} while (D3Control.Player.HpPct <= hpPct_HealthGlobe && !D3Control.Player.IsDead && D3Control.ClosestHealthGlobe != null);
					return;
				}
			}
			// Check if we need to use a potion
			if (D3Control.Player.HpPct < hpPct_UsePotion)
				D3Control.usePotion();
			// Avoid AOE if we need to and are set to.
            if (AvoidAoE && avoidAoE())
                return;
			/*
				Buff Checking
			*/
			// Storm Armor
			if (D3Control.canCast("Storm Armor") && !D3Control.HasBuff("Storm Armor"))
			{
				CastWizardSpell("Storm Armor", D3Control.Player.Location);
			}
			// Ice Armor
			if (!D3Control.HasBuff("Ice Armor") && D3Control.canCast("Ice Armor"))
			{
				CastWizardSpell("Ice Armor", D3Control.Player.Location);
			}
			// Energy Armor
			if (!D3Control.HasBuff("Energy Armor") && D3Control.canCast("Energy Armor"))
			{
				CastWizardSpell("Energy Armor", D3Control.Player.Location);
			}
			// Magic Weapon
			if (MagicWeaponTimer.IsReady && CastWizardSpell("Magic Weapon", D3Control.Player.Location))
			{
				MagicWeaponTimer.Reset();
			}
			// Familiar
			if (D3Control.canCast("Familiar") && familiarTimer.IsReady)
			{
				D3Control.CastLocationSpell("Familiar", D3Control.Player.Location, true);
				familiarTimer.Reset();
			}
			// If we are close enough to attack or if we are not moving when the moving thread is active and the target is still alive ATTACK!
            if (!D3Control.LOS(target.Location) || (target.ID == 218947 && target.DistanceFromPlayer <= 15))
            {
				/*
					ARCHON CODE
				*/			
				bool castSuccessfully = false;
				// Go in to Archon mode
				if (D3Control.canCast("Archon"))
				{
					D3Control.CastLocationSpell("Archon", D3Control.Player.Location, true);
					D3Control.output("ARCHON MODE ACTIVATED!");
				}
				// Arcane Blast
				if (target.DistanceFromPlayer <= arcaneBlastRange)
				{
					if (D3Control.CastLocationSpell("Arcane Blast", D3Control.Player.Location, true))
					{
						D3Control.output("Arcane Blast!");
						Thread.Sleep(150);
					}
				}
				// Arcane Strike
				if (D3Control.canCast("Arcane Strike") && target.DistanceFromPlayer <= arcaneStrikeRange)
				{
					if (D3Control.CastLocationSpell("Arcane Strike", D3Control.Player.Location, true))
					{
						D3Control.output("Arcane Strike!");
						Thread.Sleep(150);
					}
				}
				// Disintegration Wave
				if (target.DistanceFromPlayer <= maxDisintegrationWaveRange && target.DistanceFromPlayer >= minDisintegrationWaveRange)
				{
					NewCastWizardTargetSpell("Disintegration Wave", target, out castSuccessfully);
					if (castSuccessfully)
					{
						D3Control.output("Disintegration Wave Target!!");
						Thread.Sleep(50);
					}
				}
				/*
					END ARCHON CODE
				*/
				// Teleport
				if (!D3Control.LOS(target.Location) && !D3Control.pureLOS(target.Location) && target.DistanceFromPlayer > 10 && target.DistanceFromPlayer <= 40 && teleportTimer.IsReady && D3Control.canCast("Teleport"))
				{
					CastWizardSpell("Teleport", target.Location);
					teleportTimer.Reset();
				}
				// Hydra
				if (D3Control.isObjectValid(target) && D3Control.canCast("Hydra"))
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
				// Blizzard
				if (blizzardTimer.IsReady && D3Control.canCast("Blizzard") && D3Control.Player.ArcanePower >= ArcanePower_Blizzard)
				{
					NewCastWizardTargetSpell("Blizzard", target, out castSuccessfully);
                    if (!castSuccessfully)
                    {   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
                    }
					blizzardTimer.Reset();
					return;
				}
				if (handleAOE(target.Location))
					Thread.Sleep(5);
				// Spectral Blade
				if (D3Control.canCast("Spectral Blade") && target.DistanceFromPlayer <= 15)
				{
					NewCastWizardTargetSpell("Spectral Blade", target, out castSuccessfully);
                    if (!castSuccessfully)
                    {   // cannot cast the spell, such as obstacle, untargetable?
						moveCloser(target);
                    }
				}
				// Arcane Orb
				if (D3Control.canCast("Arcane Orb"))
				{
					CastWizardTargetSpell("Arcane Orb", target);
				}
				// Ray of Frost
				if (D3Control.canCast("Ray of Frost"))
				{
					CastWizardSpell("Ray of Frost", location);
				}
				// Arcane Torrent
				if (D3Control.canCast("Arcane Torrent"))
				{
					CastWizardSpell("Arcane Torrent", location);
				}
				// Disintegrate
				if (D3Control.canCast("Disintegrate"))
				{
					CastWizardSpell("Disintegrate", location);
				}
				// Shock Pulse
				if (D3Control.canCast("Shock Pulse") && target.DistanceFromPlayer <= attackDistance)
				{
					CastWizardSpell("Shock Pulse", location);
				}
				// Electrocute
				if (D3Control.canCast("Electrocute") && target.DistanceFromPlayer <= attackDistance)
				{
					CastWizardSpell("Electrocute", location);
				}
				// Magic Missle
				if (D3Control.canCast("Magic Missile") && target.DistanceFromPlayer <= attackDistance)
				{
					CastWizardSpell("Magic Missile", location);
				}
			}
			// If we are trying to move forward, but we arn't, attack in front of ourselves to clear a path
			if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
			{
				if (ouputMode > 1)
					D3Control.output("Trying to move, but we arn't. Changing target and firing spells to clear the way");
                // Set new target to the closest target
				D3Object ClosestTarget = D3Control.ClosestEnemy;
                D3Control.TargetManager.SetAttackTarget((D3Unit)ClosestTarget);
				// Now try to attack the target in case it is a door or object that is breakable in the way.
				Vector3D loc = D3Control.getSideSpot(D3Control.Player.Location, 0, 15);
				// Arcane Blast
				if (target.DistanceFromPlayer <= arcaneBlastRange)
				{
					if (D3Control.CastLocationSpell("Arcane Blast", D3Control.Player.Location, true))
					{
						D3Control.output("Arcane Blast!");
						Thread.Sleep(150);
					}
				}
				// Arcane Strike
				if (D3Control.canCast("Arcane Strike") && target.DistanceFromPlayer <= arcaneStrikeRange)
				{
					if (D3Control.CastLocationSpell("Arcane Strike", D3Control.Player.Location, true))
					{
						D3Control.output("Arcane Strike!");
						Thread.Sleep(150);
					}
				}
				// Explosive Blast
				if (D3Control.canCast("Explosive Blast"))
				{
					CastWizardSpell("Explosive Blast", D3Control.Player.Location);
				}
				// Energy Twister
				if (D3Control.canCast("Energy Twister"))
				{
					CastWizardSpell("Energy Twister", loc);
				}
				// Shock Pulse
				if (D3Control.canCast("Shock Pulse"))
				{
					CastWizardSpell("Shock Pulse", loc);
				}
				// Spectral Blade
				if (D3Control.canCast("Spectral Blade"))
				{
					CastWizardSpell("Spectral Blade", loc);
				}
				// Electrocute
				if (D3Control.canCast("Electrocute"))
				{
					CastWizardSpell("Electrocute", loc);
				}
				// Magic Missle
				if (D3Control.canCast("Magic Missile"))
				{
					CastWizardSpell("Magic Missile", loc);
				}
			}
            // nothing in range or no LOS to target so lets move closer
            if ((!isWithinAttackDistance(target) || D3Control.LOS(target.Location)) && !target.IsDead)
            {
				if (!D3Control.isMovingWorking())
				{
					if (D3Control.LOS(target.Location) && ouputMode > 1)
					{
						D3Control.output("Not within Line of Sight, moving closer");
					}
					else if (ouputMode > 1)
					{
						D3Control.output("Not within Attack Distance, moving closer");
					}
					D3Control.MoveTo(target, 15);
				}
				return;
            }
			// Stop moving so we can attack.
			if (D3Control.isMovingWorking())
			{
				D3Control.stopMoving();
			}
			// Make sure the target is still valid and alive
			if (D3Control.curTarget == null || !D3Control.isObjectValid(D3Control.curTarget) || D3Control.curTarget.IsDead || D3Control.curTarget.Hp <= 0 || D3Control.curTarget.Hp == null)
			{
				D3Control.output("The target does not exist anymore, looking for another now.");
				return;
			}
        }
        bool avoidAoE()
        {
            if (oldSafeSpot != safeSpot)
            {
                if (moveBackTimer.IsReady)
                {
                    moveBackTimer.Reset();
                    D3Control.output("Try to avoid AoE!");
                    D3Control.ClickMoveTo(safeSpot);
                    Thread.Sleep(750);
                    return true;
                }
                oldSafeSpot = safeSpot;
            }
            return false;
        }
		// Move Closer
        void moveCloser(D3Object target)
        {
            if (target.DistanceFromPlayer > 5)
                D3Control.MoveTo(target, target.DistanceFromPlayer - 5);
        }
		// Handle AOE
		bool handleAOE(Vector3D loc)
		{
			// Wave of Force
			var mobs40 = D3Control.TargetManager.GetAroundEnemy(30).Count;
			if (mobs40 > 0)
			{
				if (D3Control.canCast("Wave of Force"))
				{
					CastWizardSpell("Wave of Force", D3Control.Player.Location);
				}
			}
			// AOE Spells for mobs within 20 radius
			var mobs20 = D3Control.TargetManager.GetAroundEnemy(20).Count;
			if (mobs20 > 0)
			{
				// Frost Nova
				if (D3Control.canCast("Frost Nova"))
				{
					if (ouputMode > 0)
						D3Control.output("Frost Nova with "+mobs20+" mobs nearby");
					CastWizardSpell("Frost Nova", D3Control.Player.Location);
					return true;
				}
				// Slow Time
				if (D3Control.canCast("Slow Time"))
				{
					CastWizardSpell("Slow Time", D3Control.Player.Location);
				}
			}
			// Energy Twister
			if (D3Control.curTarget.DistanceFromPlayer <= 30 && D3Control.canCast("Energy Twister") && D3Control.Player.ArcanePower >= ArcanePower_EnergyTwister)
			{
				CastWizardSpell("Energy Twister", loc);
				return true;
			}
			// Meteor
			if (meteorTimer.IsReady && D3Control.canCast("Meteor") && D3Control.Player.ArcanePower >= ArcanePower_Meteor)
			{
				CastWizardSpell("Meteor", loc);
				meteorTimer.Reset();
			}
			return true;
		}
        //
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
		//
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