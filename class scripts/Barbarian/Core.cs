/*
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
//
using Astronaut.Bot;
using Astronaut.Common;
using Astronaut.Scripting;
using Astronaut.Scripts.Common;
using Astronaut.D3;
using Astronaut.Monitors;
using System.Threading;
using Astronaut.Scripts;
//
namespace Astronaut.Scripts.Barbarian
{

  /// <summary>
  /// mob ID in D3 TNT bot. We use them only for convenience for human developer.
  /// As enum isn't a strong type and can be casted to anything. It's tricky to
  /// use so we prefer using (int) rather than (ID) in the whole script. We use
  /// them only the first time we create <see cref="MobClass.MobClassInfo"/>
  /// </summary>
  public enum ID
  {
    // A1
    TheButcher = 3526,
  
    // A2
    TheBoss = 3049,

    // A3
    HeartOfSin = 193077,
    BeastRakkis = 230725,
    ColossalGolgorCore = 192850,
    ColossalGolgorArreat1 = 5581,
    SiegerBreaker = 96192,
    DemonicTremor = 60722,
    HellHideTremor = 205767,
    UniqueDemonicTremor = 149344,
    HulkingPhaseBeast = 121353,
    BloatedMalachor = 189852,
    Azmodan = 89690,
    GreatHornedGoliath = 3342,
    FireMage = 4100,
  
    // A4
    Diablo = 114917,

    // others - unknow
    WintersbaneStalker = 4552,
    MaximusWeaponDemon = 249320,
    TS1 = 5984,
    TS2 = 5985,
    TS3 = 5986,
    TS4 = 5987,
    TS5 = 5988
  }

  /// <summary>
  /// Regular barbarian skill ranges
  /// </summary>
  public enum Range
  {
    R0   =  0,
    R8   =  8,
    R10  = 10,
    R15  = 15,
    R20  = 20,
    R40  = 40,
    R60  = 60
  }


  /// <summary>
  /// This class defines properties for a whole class of mob. Each mob is defined
  /// by an ID (an int) and is stored in the dictionary <see cref="MobClassInfo"/>
  /// Constructor is private and all instances are created in the static constructor.
  /// Those info are mainly used by extended methods defined in <see cref="MobHelper"/>
  /// </summary>
  public class MobClass
  {
    
    private int    Id         { get; set; }
    private string Name       { get; set; }
    public bool    IsPriority { get; private set; }
    public  int    Weight     { get; private set; }
    public  bool   IsIgnored  { get; private set; }
    public  int    Size       { get; private set; }

    public static readonly Dictionary<int, MobClass> MobClassInfo = new Dictionary<int, MobClass>();

    private MobClass(int id, string name, bool isPriority, int weight, bool isIgnored, int size)
    {
      Id = id;
      Name = name;
      IsPriority = isPriority;
      Weight = weight;
      IsIgnored = isIgnored;
      Size = size;

      MobClassInfo.Add(id, this);
    }

    // ReSharper disable ObjectCreationAsStatement
    static MobClass()
    {
      // A1
      new MobClass((int) ID.TheButcher, "The Butcher", false, 0, false, 30);

      // A3
      new MobClass((int)ID.BeastRakkis,           "Beast Rakkis",             false, 0, false, 2);
      new MobClass((int)ID.ColossalGolgorCore,    "Colossal Golgore Core",    false, 0, false, 6);
      new MobClass((int)ID.ColossalGolgorArreat1, "Colossal Golgore Arreat1", false, 0, false, 6);
      new MobClass((int)ID.BloatedMalachor,       "Bloated Malachor",         false, 0, false, 6);
      new MobClass((int)ID.DemonicTremor,         "Demonic Tremor",           false, 0, false, 6);
      new MobClass((int)ID.HellHideTremor,        "Hell Hide Tremor",         false, 0, false, 6);
      new MobClass((int)ID.UniqueDemonicTremor,   "Unique Demonic Tremor",    false, 0, false, 7);
      new MobClass((int)ID.GreatHornedGoliath,    "Great Horned Goliath",     false, 0, false, 2);
      new MobClass((int)ID.FireMage,              "Fire Mage",                false, 0, false, 0);
      new MobClass((int)ID.SiegerBreaker,         "Sieger Breaker",           false, 0, false, 10);
      new MobClass((int)ID.HulkingPhaseBeast,     "Hulking Phase Beast",      false, 0, false, 5);
      new MobClass((int)ID.WintersbaneStalker,    "Winter Bane Stalker",      false, 0, false, 5);
      new MobClass((int)ID.HeartOfSin,            "Heart of Sin",             false, 0, false, 20);
      new MobClass((int)ID.Azmodan,               "Azmodan",                  false, 0, false, 17);
    
    // A4
    new MobClass((int)ID.Diablo,                "Diablo",                   false, 0, false, 0);

      // Others
      new MobClass((int)ID.MaximusWeaponDemon, "Maximus Weapon Demon", false, 0, true, 0);
      new MobClass((int)ID.TS1,                "Treasure Seeker 1", CombatState.PrioritizeTS, 9, false, 0);
      new MobClass((int)ID.TS2,                "Treasure Seeker 2", CombatState.PrioritizeTS, 9, false, 0);
      new MobClass((int)ID.TS3,                "Treasure Seeker 3", CombatState.PrioritizeTS, 9, false, 0);
      new MobClass((int)ID.TS4,                "Treasure Seeker 4", CombatState.PrioritizeTS, 9, false, 0);
      new MobClass((int)ID.TS5,                "Treasure Seeker 5", CombatState.PrioritizeTS, 9, false, 0);
    }
    // ReSharper restore ObjectCreationAsStatement

  }


  /// <summary>
  /// Every skill is an instance of this class. Skill can either be
  /// <see cref="SkillType.Direction"/> or <see cref="SkillType.Location"/> skill.
  /// Location skills are casted on a specific <see cref="Vector3D"/> point.
  /// Skills have a name, a cost, a range, a type. Also some skills have an
  /// animation like Rend, others don't like Sprint.
  /// </summary>
  public class Skill
  {

    /// <summary>
    /// Location skills are mostly casted on a specific point. But from my
    /// experience, I would say most skills can either be casted as
    /// Direction or Location skills. If you are not sure if a skill is either
    /// Direction or Location skill, most of the times it's a Direction skill.
    /// </summary>
    public enum SkillType
    {
      Direction,
      Location
    };

    private string    Name              { get; set; }
    private int       Fury              { get; set; }
    private int       AnimationDuration { get; set; }
    private bool      HasAnimation      { get; set; }
    private Range     Range             { get; set; }
    private SkillType Type              { get; set; }

    public Skill(string name, int fury, int animationDuration, Range range, SkillType type)
    {
      Name = name;
      Fury = fury;
      AnimationDuration = animationDuration;
      HasAnimation = animationDuration > 0;
      Range = range;
      Type = type;
    }

    /// <summary>
    /// Check if this skill is still up, it should appear in buff bar
    /// </summary>
    /// <returns></returns>
    public bool IsUp()
    {
      return D3Control.HasBuff(Name);
    }

    /// <summary>
    /// Check if this skill is on cooldown
    /// </summary>
    /// <returns></returns>
    public bool IsOnCD()
    {
      return !D3Control.isOnCD(Name);
    }

    /// <summary>
    /// Check if this skill is in skill set, not on CD and player got enough fury
    /// </summary>
    /// <returns></returns>
    public bool CanCast()
    {
      if (!D3Control.canCast(Name))
        return false;

      if (IsOnCD())
        return false;

      if (Fury == 0)
        return true;

      if (CombatState.SaveFury)
        return false;

      if (D3Control.Player.Fury < Fury)
        return false;

      return true;
    }

    public bool NotEnoughFury()
    {
      return D3Control.Player.Fury < Fury;
    }

    public bool IsInSkillSet()
    {
      return D3Control.canCast(Name);
    }

    public bool IsAvalaible()
    {
      return IsInSkillSet() && !IsOnCD();
    }

    /// <summary>
    /// Check if current target is in skill range and range is not equal to 0.
    /// This method use <see cref="CombatState.DistanceToPlayer"/> to get mob
    /// distance to player
    /// </summary>
    /// <returns></returns>
    public bool IsTargetInRange()
    {
      return Range == Range.R0 || CombatState.DistanceToPlayer(D3Control.curTarget) <= (float)Range;
    }

    /// <summary>
    /// Check if an elite is in skill range and range is not equal to 0.
    /// Mob distance to player is calculated with <see cref="CombatState.DistanceToPlayer"/>
    /// </summary>
    /// <returns></returns>
    public bool IsEliteInRange()
    {
      return Range == Range.R0 || CombatState.Elites[Range] > 0;
    }

    /// <summary>
    /// Check if a trash mob is in skill range and range is not equal to 0.
    /// Mob distance to player is calculated with <see cref="CombatState.DistanceToPlayer"/>
    /// </summary>
    /// <returns></returns>
    public bool IsTrashMobInRange()
    {
      return Range == Range.R0 || CombatState.TrashMobs[Range] > 0;
    }

    /// <summary>
    /// Check if any mob, trash or elite, is in skill range and range is not equal to 0.
    /// Mob distance to player is calculated with <see cref="CombatState.DistanceToPlayer"/>
    /// </summary>
    /// <returns></returns>
    public bool IsMobInRange()
    {
      return IsTrashMobInRange() || IsEliteInRange();
    }

    public void Cast()
    {
      Cast(D3Control.Player.Location);
    }

    public void Cast(Vector3D loc, bool b = true)
    {
      if (Type == SkillType.Direction)
        D3Control.CastDirectionSpell(Name, loc);
      else
        D3Control.CastLocationSpell(Name, loc, b);

      if (HasAnimation)
        Thread.Sleep(AnimationDuration);
    }
  }

  public class Core : GlobalBaseBotState
  {
    //
    protected override void DoEnter(D3Player entity)
    {
      base.DoEnter(entity);
      combatState = new CombatState();
			string modified_date = "3/3/2013";
			D3Control.output("ASWeiler's "+D3Control.playerClass+" Class Script (last modified "+modified_date+").");
    }

    protected override bool IsHealer() { return false; }
    public static void Emergency(D3Player player) { }
    protected void CastInterruption() { }
    public static void DebuffAll() { }
    public override bool NeedRest(D3Player player) { return false; }
  }
  //
  public class CombatState : Common.CombatState
  {
    /*
        CONFIG / OPTIONS / SETTINGS AREA
    */
    // [CONFIG]
    private static int  HealthPotHpPct          = 50;    // below this % health the script will try to use a health potion
    private static int  HealthGlobeHpPct        = 50;    // below this % the script will try to obtain an health globe
    private static bool MoveForHealthGlobe      = true;  // move to pick up an healthGlobe
    private static int  PickupHealthGlobeRadius = 20;    // pickup health globe in this radius only, don't set it too high
    private static bool UseLeapHealthGlobe      = false; // Leap to health globe
    private static bool AOEAvoidEnabled         = true;  // try to avoid AoE (desecrate and middle of arcane beams)
    private static int  RendTime                = 2500;  // default time in milliseconds between uses of Rend (Fury permitting) (2500)
    private static int  IgnorePainPct           = 50;    // below this % life the script will try to use ignore pain (if available)
    public  static bool PrioritizeTS            = true;  // prioritize treasure seekers
    private static bool SaveFuryEnabled         = true;  // save fury for wrath of the berserker, earthquake and call of the ancients for elites only
    private static int  WWMinFury               = 20;     // minimum fury to use Whirlwind, 0 = Used ASAP
    private static int  HotaMinFury             = 80;    // minimum fury to use Hammer of the Ancients, 60 for 100-115 fury, 70 for 116-130, 80 over 130
    // [/CONFIG]

    // 		"Skill Name", Fury Cost, Animation Duration, Range (0 = Disabled), Skill.SkillType.Direction or Location)
    static Skill Bash              = new Skill("Bash",                    0, 150, Range.R10, Skill.SkillType.Direction);
    static Skill Cleave            = new Skill("Cleave",                  0, 200, Range.R10, Skill.SkillType.Direction);
    static Skill Frenzy            = new Skill("Frenzy",                  0, 75,  Range.R10, Skill.SkillType.Direction);
    static Skill Hota              = new Skill("Hammer of the Ancients", 20, 100, Range.R10, Skill.SkillType.Direction);
    static Skill Rend              = new Skill("Rend",                   20, 200, Range.R15, Skill.SkillType.Direction);
    static Skill SiesmicSlam       = new Skill("Siesmic Slam",           30, 100, Range.R15, Skill.SkillType.Direction);
    static Skill Whirlwind         = new Skill("Whirlwind",              10,   0, Range.R20,  Skill.SkillType.Location);
    static Skill GroundStomp       = new Skill("Ground Stomp",            0, 200, Range.R10, Skill.SkillType.Direction);
    static Skill Leap              = new Skill("LeapAttack",              0, 100, Range.R0,  Skill.SkillType.Location);
    static Skill Sprint            = new Skill("Sprint",                 20,   0, Range.R0,  Skill.SkillType.Direction);
    static Skill IgnorePain        = new Skill("Ignore Pain",             0,   0, Range.R0,  Skill.SkillType.Direction);
    static Skill AncientSpear      = new Skill("Ancient Spear",           0, 200, Range.R20,  Skill.SkillType.Location);
    static Skill Revenge           = new Skill("Revenge",                 0, 200, Range.R15, Skill.SkillType.Direction);
    static Skill FuriousCharge     = new Skill("Furious Charge",          0, 200, Range.R0,  Skill.SkillType.Location);
    static Skill Overpower         = new Skill("Overpower",               0,   0, Range.R8,  Skill.SkillType.Direction);
    static Skill WeaponThrow       = new Skill("Weapon Throw",           10, 150, Range.R20, Skill.SkillType.Location);
    static Skill ThreateningShout  = new Skill("Threatening Shout",       0,   0, Range.R15, Skill.SkillType.Direction);
    static Skill BattleRage        = new Skill("Battle Rage",            20,   0, Range.R0,  Skill.SkillType.Direction);
    static Skill WarCry            = new Skill("War Cry",                 0,   0, Range.R0,  Skill.SkillType.Direction);
    static Skill Earthquake        = new Skill("Earthquake",             50, 200, Range.R10, Skill.SkillType.Direction);
    static Skill CallOfTheAncients = new Skill("Call of the Ancients",   50, 200, Range.R20, Skill.SkillType.Direction);
    static Skill Wotb              = new Skill("Wrath of the Berserker", 50,   0, Range.R20, Skill.SkillType.Direction);


    public static bool SaveFury;
    private const int CombatTime = 10;
    static Thread keepSpellThread;
    static Vector3D SafeSpot;
    private static int PlayerHpPct;
    private const float RegularPullingDistance = 5.0f;
    private static bool _inited;

    static readonly CWSpellTimer RendTimer           = new CWSpellTimer(RendTime);
    static readonly CWSpellTimer AvoidAOETimer       = new CWSpellTimer(2 * 1000);
    static readonly CWSpellTimer UpdateSafeSpotTimer = new CWSpellTimer((int)(0.5 * 1000));
    static readonly CWSpellTimer BattleRageTimer     = new CWSpellTimer(90 * 1000);
    static readonly CWSpellTimer SwitchTargetTimer   = new CWSpellTimer((int)(0.5 * 1000));
    static readonly CWSpellTimer PotionTimer         = new CWSpellTimer(30 * 1000);
    static readonly CWSpellTimer StuckTimer          = new CWSpellTimer(2 * 1000);

    public static readonly Dictionary<Range, int> TrashMobs = new Dictionary<Range, int>();
    public static readonly Dictionary<Range, int> Elites    = new Dictionary<Range, int>();


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
      catch (ThreadStateException)
      {
      }

      keepSpellThread = new Thread(KeepSpell);
      keepSpellThread.IsBackground = true;
      keepSpellThread.Start();
      while (!keepSpellThread.IsAlive)
      {
      }
    }

    private static void CheckBuildVars()
    {
      if (Whirlwind.IsInSkillSet())
      {
        MoveForHealthGlobe = false;
        AOEAvoidEnabled = false;
        SaveFuryEnabled = false;
      }
    }

    private static void InitMobs()
    {
      foreach (Range r in Enum.GetValues(typeof (Range)))
      {
        TrashMobs.Add(r, 0);
        Elites.Add(r, 0);
      }
    }

    protected override void DoEnter(D3Player entity)
    {
      if (_inited)
        return;

      _inited = true;
      InitMobs();
      CheckBuildVars();
    }

    private static void KeepSpell()
    {
      var rgen = new Random();
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
          // Get our mobs info
          GetMobsInfo();
          if (WarCry.CanCast() && !Sprint.IsUp() && D3Control.isMovingWorking())
            WarCry.Cast();

          if (Sprint.CanCast() && !Sprint.IsUp() && (D3Control.isMovingWorking() || D3Control.Player.isInCombat))
            Sprint.Cast();

          // D3Control.output("[X: " + D3Control.Player.Location.X + "] [Y: " + D3Control.Player.Location.Y + "] [Z: " + D3Control.Player.Location.Z + "]");
          if (CombatState.CastBuff())
          {
            Thread.Sleep(300);
            continue;
          }
          if (CombatState.CastAOE())
          {
            Thread.Sleep(300);
            continue;
          }
          if (CombatState.HasMovedToHealthGlobe())
          {
            Thread.Sleep(300);
            continue;
          }
          Thread.Sleep(300);
        }
      }
      catch (Exception)
      {
      }
    }

    private static Range GetRange(float distance)
    {
      var all = new[] {Range.R8, Range.R10, Range.R15, Range.R20, Range.R40, Range.R60};

      // range from R8 -> R60 in order
      foreach (Range range in all)
      {
        if (distance <= (float) range)
          return range;
      }
      // If we did not return a range from our loop, return a default of 60.
      return Range.R60;
    }

    /// <summary>
    /// Get info on mobs in 60 feet radius. Thoses info are mainly used for AOE
    /// and buffs.
    /// </summary>
    private static void GetMobsInfo()
    {
      foreach (Range r in Enum.GetValues(typeof (Range)))
      {
        Elites[r]    = 0;
        TrashMobs[r] = 0;
      }

      var mobs = D3Control.TargetManager.GetAroundEnemy(60f);
      foreach (var mob in mobs)
      {
        Range range = GetRange(DistanceToPlayer(mob));
        if (mob.IsElite)
          Elites[range]++;
        else
          TrashMobs[range]++;
      }

      TrashMobs[Range.R10] += TrashMobs[Range.R8];
      TrashMobs[Range.R15] += TrashMobs[Range.R10];
      TrashMobs[Range.R20] += TrashMobs[Range.R15];
      TrashMobs[Range.R40] += TrashMobs[Range.R40];
      TrashMobs[Range.R60] += TrashMobs[Range.R60];

      Elites[Range.R10] += Elites[Range.R8];
      Elites[Range.R15] += Elites[Range.R10];
      Elites[Range.R20] += Elites[Range.R15];
      Elites[Range.R40] += Elites[Range.R40];
      Elites[Range.R60] += Elites[Range.R60];
    }

    /// <summary>
    /// Get the closest mob in 40 yards radius for trash, 60 for elite
    /// 60 yards is about one full screen length.
    /// If player is stuck there are no more priority mobs
    /// </summary>
    /// <returns>a mob on some criteria</returns>
    private static D3Unit GetTarget()
    {
      D3Unit target          = null;
      float  maxDistance     = 60f;
      bool   priorityEnabled = !IsStuck();

      // [TEMP] verifier l'ordre de mobs
      var mobs = D3Control.TargetManager.GetAroundEnemy(maxDistance);
      foreach (var mob in mobs)
      {
        if (mob.IsDead || D3Control.pureLOS(mob.Location) || mob.IsInvulnerable || mob.Untargetable || !mob.IsSelectable || !mob.IsAttackable || IsIgnored(mob))
          continue;

        if (priorityEnabled && IsPriority(mob))
          return mob;

        float mobDistance = DistanceToPlayer(mob);
        if (mobDistance < maxDistance)
        {
          if (mob.IsElite)
          {
            maxDistance = mobDistance;
            target = mob;
            continue;
          }

          if (mobDistance < 40f)
          {
            maxDistance = mobDistance;
            target = mob;
          }
        }
      }

      return target;
    }

    /// <summary>
    /// Set player stuck status. If true, player could be stuck. Before any
    /// movement, stuck status should be checked with <see cref="IsStuck"/>
    /// </summary>
    /// <param name="b">stuck status</param>
    private static void SetStuck(bool b)
    {
      if (b)
        StuckTimer.Reset();
      else
        StuckTimer.Set();
    }

    /// <summary>
    /// Check if previous player movement has failed. If true, player could be
    /// stuck. Player is stuck for <see cref="StuckTimer"/> duration max.
    /// (default is 1 sec)
    /// </summary>
    /// <returns></returns>
    private static bool IsStuck()
    {
      return !StuckTimer.IsReady;
    }

    /// <summary>
    /// Set the new target to <see cref="D3Control.TargetManager"/>
    /// </summary>
    /// <returns>true if we got a target</returns>
    private static bool IsTarget()
    {
      D3Control.TargetManager.ClearTarget();
      var target = GetTarget();

      if (target != null)
      {
        SetTarget(target);
        return true;
      }
      return false;
    }

    /// <summary>
    /// Update <see cref="SafeSpot"/> for AOE avoidance
    /// </summary>
    static void UpdateAOEInfo()
    {
      if (UpdateSafeSpotTimer.IsReady)
      {
        SafeSpot = D3Control.getSafePoint(D3Control.getDangourousTargetInfo());
        UpdateSafeSpotTimer.Reset();
      }
    }


    /// <summary>
    /// Method called whenever Bot need to avoid an AOE. This method check 
    /// <see cref="SafeSpot"/>. To get a <see cref="SafeSpot"/> see <see cref="UpdateAOEInfo"/>.
    /// <see cref="SafeSpot"/> is set to null.
    /// The kind of AOE detected depends on the TNT program settings. This method
    /// only handle AOE avoidance during combat. <see cref="AvoidAOETimer"/>
    /// prevent avoiding AOE too often. Skills like <see cref="Leap"/> would be
    /// used first to avoid AOE otherwise bot will try to move.
    /// If bot get stuck, he will force switch target.
    /// </summary>
    /// <returns>true if any skill has been casted or any movement initiated</returns>
    static bool AvoidAOE()
    {
      if (!AOEAvoidEnabled || SafeSpot == null || !AvoidAOETimer.IsReady)
        return false;

      var s = SafeSpot;
      SafeSpot = null;

      // [TEMP] vector in range
      if (FuriousCharge.CanCast())
      {
        D3Control.output("Furious charge to avoid AoE");
        FuriousCharge.Cast(s);
        AvoidAOETimer.Reset();
        return true;
      }

      // [TEMP] vector in range
      if (Leap.CanCast())
      {
        D3Control.output("Leap to avoid AoE");
        Leap.Cast(s);
        AvoidAOETimer.Reset();
        return true;
      }

      if (IsStuck())
        return false;

      D3Control.output("Try to avoid AoE");
      if (!MoveTo(s, 3))
        RequireSwitchTarget();
      AvoidAOETimer.Reset();
      return true;
    }

    /// <summary>
    /// Each time a target is set, <see cref="SwitchTargetTimer"/> is reset,
    /// so we ensure we won't stay on this target for too long.
    /// </summary>
    /// <param name="u">the new target</param>
    private static void SetTarget(D3Unit u)
    {
      D3Control.output("[ID: " + u.ID + "] [HP: " + (int)u.Hp + "] [D: " + u.DistanceFromPlayer.ToString("0.00") + " | " + DistanceToPlayer(u).ToString("0.00") + "]");
      D3Control.TargetManager.SetAttackTarget(u);
      SwitchTargetTimer.Reset();
    }

    /// <summary>
    /// Main method called when bot enter combat mode.
    /// Bot stay for maximum 10 sec in this loop.
    /// Loop performs those actions in order until no targets left or timer is ready:
    /// 1) get dungeon and player info
    /// 2) do a single action
    /// 3) get a target
    /// This loop should be the only loop in the whole process, only one action
    /// should be performed per iteration. (1 attack or 1 move)
    /// We do not switch target at each loop iteration, but only when target is
    /// dead or when <see cref="SwitchTargetTimer"/> is ready.
    /// <see cref="IsTarget"/> is called one time at beginning of the method
    /// because we pick target on some criteria and this method could return
    /// false even if bot has a target.
    /// </summary>
    /// <param name="entity">the player</param>
    protected override void DoExecute(D3Player entity)
    {
      var timer = new CWSpellTimer(CombatTime*1000, false);
      var u = D3Control.curTarget;
      if (!IsTarget())
        SetTarget(u);
      // Start the loop for CombatMode
      while (true)
      {
        if (!D3Control.IsInGame() || D3Control.Player.IsDead || timer.IsReady)
          break;
        // If we are not in combat or the current target returns as null, clear our target list and force a exit out of Combat Mode.
        if (!D3Control.Player.isInCombat && D3.D3Control.curTarget == null)
        {
          D3Control.output("Out of combat?");
          D3Control.TargetManager.ClearTarget();
          DoExit(D3Control.Player);
          return;
        }
        // Check for loot if we need to (the bot has a timer for 2 seconds built in)
        D3Control.checkLoots();
        // Update the map info
        D3Control.updateDungoneInfo();
        // Update the player info
        D3Control.Player.reloadPlayerInfo();
        // Update the real HP %
        CheckHpPct();
        // Do our single action
        DoSingleAction();
        // So long as we have not hit the SwitchTargetTimer's time yet, keep going through our CombatMode loop.
        if (!SwitchTargetTimer.IsReady)
          continue;
        // If there is no target, break out of the CombatMode loop.
        if (!IsTarget())
          break;
      }
      // Since we left the loop, reset the SaveFury bool.
      SaveFury = false;
      // Since we have no target left, output it for the All tab on D3TNT to display to the end user.
      D3Control.output("No target found, returning from DoExecute.");
    }

    /// <summary>
    /// Set <see cref="SwitchTargetTimer"/> to ready.
    /// We switch target only when previous target is dead or when
    /// <see cref="SwitchTargetTimer"/> is ready.
    /// </summary>
    private static void RequireSwitchTarget()
    {
       SwitchTargetTimer.Set();
    }

    /// <summary>
    /// Save fury for wotb, call of the ancients or earthquake if needed. We only
    /// cast those skills on elites.
    /// </summary>
    private static void CheckSaveFury()
    {
      SaveFury = false;
      if (!SaveFuryEnabled)
        return;

      if (Wotb.IsAvalaible() && Wotb.NotEnoughFury() && Wotb.IsEliteInRange())
      {
        SaveFury = true;
        return;
      }

      if (CallOfTheAncients.IsAvalaible() && CallOfTheAncients.NotEnoughFury() && CallOfTheAncients.IsEliteInRange())
      {
        SaveFury = true;
        return;
      }

      if (Earthquake.IsAvalaible() && Earthquake.NotEnoughFury() && Earthquake.IsEliteInRange())
      {
        SaveFury = true;
      }
    }

    /// <summary>
    /// Bot does only a single action in this method then return.
    /// Action can be: [avoid AOE, cast 1 spell, MoveTo]
    /// This allow us to prioritize some skills on others.
    /// In order we prioritize:
    /// 1) AOE avoidance because there is a timer, it will be casted only one time until timer is ready
    /// If we had choosen to cast spell before AOE avoidance, we would spam our spells and never avoid AOE.
    /// 2) buffs, they all have cooldown, so we can't spam them.
    /// 3) AOE skills, most of them can heal like Rend or Revenge.
    /// 4) move for health globe before fury dump skills to prioritize life,
    /// MoveForHealthGlobe should be set to false if got enough leech.
    /// 5) fury dump like Hammer of the ancients.
    /// 6) free skills.
    /// 7) get closer to target
    /// </summary>
    static void DoSingleAction()
    {
      // Update the AOE info
      UpdateAOEInfo();
      // If there is some AOE to avoid, get out of it and return to the start of DoSingleAction.
      if (AvoidAOE())
        return;
      // Set u as our current target variable's shortcut variable
      var u = D3Control.curTarget;
      // If the current target returns as null or is not a valid object, or is dead, force getting a new taret set as our current target and return from DoSingleAction.
      if (u == null || !D3Control.isObjectValid(u) || u.IsDead)
      {
        RequireSwitchTarget();
        //SetStuck(false);
        return;
      }
      // Check if we need to save Fury or not.
      CheckSaveFury();
      // Handle any boss fights if needed
      D3Control.TargetManager.handleSpecialFight();
      // Do our player checked before starting a fight
      if (!GlobalBaseBotState.checkBeforePull(D3Control.Player))
        return;
      // Cast a spell that are location based
      if (CastLocationSkill())
        return;
      // Cast a Secondary spell
      if (CastSecondary())
        return;
      // If we cast a Primary Spell, return from the DoSingleAction till we do not cast one anymore.
      if (CastPrimary())
        return;
      // Set that we are not stuck since we got through all of our checks.
      SetStuck(false);
      // If we cannot move to our current target, force switching to a new target
      if (!MoveTo(u, RegularPullingDistance + Size(u)))
        RequireSwitchTarget();
    }

    static bool CastBuff()
    {
      if (BattleRage.CanCast())
      {
        if (!BattleRage.IsUp() || (D3Control.Player.Fury > 120 && BattleRageTimer.IsReady))
        {
          BattleRage.Cast();
          BattleRageTimer.Reset();
          return true;
        }
      }

      if (WarCry.CanCast())
      {
        WarCry.Cast();
        return true;
      }

      if (ThreateningShout.CanCast() && ThreateningShout.IsMobInRange())
      {
        ThreateningShout.Cast();
        return true;
      }

      if (IgnorePain.CanCast() && PlayerHpPct < IgnorePainPct)
      {
        IgnorePain.Cast();
        return true;
      }

      if (Wotb.CanCast() && Wotb.IsEliteInRange())
      {
        SaveFury = false;
        Wotb.Cast();
        return true;
      }

      if (CallOfTheAncients.CanCast() && CallOfTheAncients.IsEliteInRange())
      {
        SaveFury = false;
        CallOfTheAncients.Cast();
        return true;
      }

      return false;
    }

    static bool CastAOE()
    {
      if (Revenge.CanCast() && Revenge.IsMobInRange())
      {
        Revenge.Cast();
        return true;
      }

      if (Rend.CanCast() && RendTimer.IsReady && Rend.IsMobInRange())
      {
        Rend.Cast();
        RendTimer.Reset();
        return true;
      }

      if (Overpower.CanCast() && Overpower.IsMobInRange())
      {
        Overpower.Cast();
        return true;
      }

      if (Earthquake.CanCast() && Earthquake.IsEliteInRange())
      {
        SaveFury = false;
        Earthquake.Cast();
        return true;
      }

      if (GroundStomp.CanCast() && GroundStomp.IsMobInRange())
      {
        GroundStomp.Cast();
        return true;
      }

      return false;
    }

    static bool CastSecondary()
    {
      var target = D3Control.curTarget;

      if (Whirlwind.CanCast() && Whirlwind.IsTargetInRange() && D3Control.Player.Fury > WWMinFury)
      {
        Whirlwind.Cast(D3Control.getTargetSideSpot(target.Location, 0, 11));
        return true;
      }

      if (Hota.CanCast() && Hota.IsTargetInRange() && D3Control.Player.Fury > HotaMinFury)
      {
        Hota.Cast(target.Location);
        return true;
      }

      if (SiesmicSlam.CanCast() && SiesmicSlam.IsTargetInRange())
      {
        SiesmicSlam.Cast(target.Location);
        return true;
      }

      if (WeaponThrow.CanCast() && WeaponThrow.IsTargetInRange())
      {
        WeaponThrow.Cast(D3Control.getTargetSideSpot(target.Location, 0, 2));
        return true;
      }

      return false;
    }

    /// <summary>
    /// Try to get an health globe. First bot try to use <see cref="Leap"/> or
    /// <see cref="FuriousCharge"/> then bot try to move.
    /// </summary>
    /// <returns>true if bot has casted a skill or initiated a movement to get an health globe</returns>
    private static bool HasMovedToHealthGlobe()
    {
      if (PlayerHpPct > HealthGlobeHpPct || !MoveForHealthGlobe)
        return false;

      D3Object healthGlobe = D3Control.ClosestHealthGlobe;
      if (healthGlobe == null)
        return false;

      if (D3Control.Player.DistanceTo(healthGlobe) > PickupHealthGlobeRadius)
        return false;

      // adjust the landing point so we land just past it, this should pick it up on landing // [TEMP] in range
      if (Leap.CanCast() && UseLeapHealthGlobe)
      {
        var loc = healthGlobe.Location;

        if (D3Control.Player.Location.X <= loc.X)
          loc.X++;
        else
          loc.X--;

        if (D3Control.Player.Location.Y <= loc.Y)
          loc.Y++;
        else
          loc.Y--;

        D3Control.output("Leap to health globe! Target X: " + loc.X + " Y: " + loc.Y);
        Leap.Cast(loc);
        return true;
      }

      if (IsStuck())
        return false;

      D3Control.output("Move to health globe");
      if (!MoveTo(healthGlobe, 2.5f))
        RequireSwitchTarget();
      return true;
    }

    private static bool CastLocationSkill()
    {
      var target = D3Control.curTarget;

      // [TEMP] in range
      if (Leap.CanCast())
      {
        Leap.Cast(target.Location);
        return true;
      }

      // [TEMP] in range
      if (FuriousCharge.CanCast() && DistanceToPlayer(target) <= 40)
      {
        FuriousCharge.Cast(target.Location);
        return true;
      }

      if (AncientSpear.CanCast() && AncientSpear.IsTargetInRange())
      {
        AncientSpear.Cast(target.Location);
        return true;
      }

      return false;
    }

    private static bool CastPrimary()
    {
      var target = D3Control.curTarget;

      if (Frenzy.CanCast() && Frenzy.IsTargetInRange())
      {
        Frenzy.Cast(target.Location);
        return true;
      }

      if (Bash.CanCast() && Bash.IsTargetInRange())
      {
        Bash.Cast(target.Location);
        return true;
      }

      if (Cleave.CanCast() && Cleave.IsTargetInRange())
      {
        Cleave.Cast(target.Location);
        return true;
      }

      return false;
    }

    /// <summary>
    /// <see cref="PlayerHpPct"/> should be used to get Player health.
    /// This method set <see cref="PlayerHpPct"/> and drink a potion if needed.
    /// </summary>
    private static void CheckHpPct()
    {
      PlayerHpPct = D3Control.Player.HpPct;
      if (PlayerHpPct <= -100 || PlayerHpPct >= 100)
        PlayerHpPct = 100;

      if (PlayerHpPct <= HealthPotHpPct && PotionTimer.IsReady)
      {
        D3Control.usePotion();
        PotionTimer.Reset();
      }
    }
    
    /// <summary>
    /// Move to an object <paramref name="o"/> until we reach distance
    /// <paramref name="r"/>.
    /// </summary>
    /// <param name="o">object player is moving to</param>
    /// <param name="r">distance at which player should approach the object</param>
    /// <returns>false if we get stuck while moving, true otherwise</returns>
    private static bool MoveTo(D3Object o, float r)
    {
      D3Control.MoveTo(o, r);
      Thread.Sleep(200);
      if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
      {
        SetStuck(true);
        D3Control.output("Stuck while moving to target");
        return false;
      }
      return true;
    }

    /// <summary>
    /// Try to move to position <paramref name="v"/> <paramref name="n"/> times.
    /// We split the movement in <paramref name="n"/> movement to detect if we
    /// get stuck while moving.
    /// </summary>
    /// <param name="v">location we are moving to</param>
    /// <param name="n">number of moves to try to reach the location</param>
    /// <returns>false if we get stuck while moving, true otherwise</returns>
    private static bool MoveTo(Vector3D v, int n = 1)
    {
      if (n <= 1)
        n = 1;

      for (int i = 0; i < n; i++)
      {
        D3Control.ClickMoveTo(v);
        Thread.Sleep(200);
        if (D3Control.isMovingWorking() && !D3Control.Player.isMovingForward)
        {
          SetStuck(true);
          D3Control.output("Stuck while moving to position: " + v.X + "," + v.Y + "," + v.Z);
          return false;
        }
      }
      return true;
    }

    public static bool IsIgnored(D3Unit u)
    {
      return MobClass.MobClassInfo.ContainsKey(u.ID) && MobClass.MobClassInfo[u.ID].IsIgnored;
    }

    public static bool IsPriority(D3Unit u)
    {
      return MobClass.MobClassInfo.ContainsKey(u.ID) && MobClass.MobClassInfo[u.ID].IsPriority;
    }

    /// <summary>
    /// Return the mob size if mob size is set otherwise 0. It's used to handle
    /// large mobs.
    /// </summary>
    /// <param name="u">a mob</param>
    /// <returns>mob size</returns>
    public static int Size(D3Unit u)
    {
      return MobClass.MobClassInfo.ContainsKey(u.ID) ? MobClass.MobClassInfo[u.ID].Size : 0;
    }

    /// <summary>
    /// This method return the real distance from a mob to player adjusted with 
    /// mob size. See <see cref="Size"/>.
    /// This method should be used instead of <see cref="D3Object.DistanceFromPlayer"/>
    /// </summary>
    /// <param name="u">a mob</param>
    /// <returns>mob distance to player</returns>
    public static float DistanceToPlayer(D3Unit u)
    {
      return u.DistanceFromPlayer - Size(u);
    }

    protected override void DoExit(D3Player entity)
    {
      D3Control.output("DoExit");
      if (PreviousState != null)
        CallChangeStateEvent(entity, PreviousState, false, false);
    }

    protected override void DoFinish(D3Player entity)
    {
      D3Control.output("DoFinish");
    }
  }
}
  