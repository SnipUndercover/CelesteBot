﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste;
using Celeste.Mod;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.Utils;
using static CelesteBot_2023.CelesteBotMain;

namespace CelesteBot_2023.SimplifiedGraphics;

public static class SimplifiedGraphicsFeature
{
    private static readonly List<string> SolidDecals = new() {
        "3-resort/bridgecolumn",
        "3-resort/bridgecolumntop",
        "3-resort/brokenelevator",
        "3-resort/roofcenter",
        "3-resort/roofcenter_b",
        "3-resort/roofcenter_c",
        "3-resort/roofcenter_d",
        "3-resort/roofedge",
        "3-resort/roofedge_b",
        "3-resort/roofedge_c",
        "3-resort/roofedge_d",
        "4-cliffside/bridge_a",
    };

    private static readonly bool lastSimplifiedGraphics = CelesteBotMain.Settings.SimplifiedGraphics;
    private static SolidTilesStyle currentSolidTilesStyle;
    private static bool creatingSolidTiles;

    [Initialize]
    private static void Initialize()
    {
        // Optional: Various graphical simplifications to cut down on visual noise.
        //On.Celeste.Level.Update += Level_Update;

        if (ModUtils.GetType("FrostHelper", "FrostHelper.CustomSpinner") is { } customSpinnerType)
        {
            foreach (ConstructorInfo constructorInfo in customSpinnerType.GetConstructors())
            {
                constructorInfo.IlHook(ModCustomSpinnerColor);
            }
        }

        if (ModUtils.GetType("MaxHelpingHand", "Celeste.Mod.MaxHelpingHand.Entities.RainbowSpinnerColorController")?.GetMethodInfo("getModHue") is
            { } getModHue)
        {
            getModHue.IlHook(ModRainbowSpinnerColor);
        }

        if (ModUtils.GetType("SpringCollab2020", "Celeste.Mod.SpringCollab2020.Entities.RainbowSpinnerColorController")?.GetMethodInfo("getModHue") is
            { } getModHue2)
        {
            getModHue2.IlHook(ModRainbowSpinnerColor);
        }

        if (ModUtils.GetType("VivHelper", "VivHelper.Entities.CustomSpinner")?.GetMethodInfo("CreateSprites") is
            { } customSpinnerCreateSprites)
        {
            customSpinnerCreateSprites.IlHook(ModVivCustomSpinnerColor);
        }

        if (ModUtils.GetType("PandorasBox", "Celeste.Mod.PandorasBox.TileGlitcher")?.GetMethodInfo("tileGlitcher") is
            { } tileGlitcher)
        {
            tileGlitcher.GetStateMachineTarget().IlHook(ModTileGlitcher);
        }

        Type t = typeof(SimplifiedGraphicsFeature);

        //On.Celeste.CrystalStaticSpinner.CreateSprites += CrystalStaticSpinner_CreateSprites;
        IL.Celeste.CrystalStaticSpinner.GetHue += CrystalStaticSpinnerOnGetHue;

        HookHelper.SkipMethod(t, nameof(IsSimplifiedGraphics), "Render", typeof(MirrorSurfaces));

        IL.Celeste.LightingRenderer.Render += LightingRenderer_Render;
        On.Celeste.ColorGrade.Set_MTexture_MTexture_float += ColorGradeOnSet_MTexture_MTexture_float;
        IL.Celeste.BloomRenderer.Apply += BloomRendererOnApply;

        On.Celeste.Decal.Render += Decal_Render;
        HookHelper.SkipMethod(t, nameof(IsSimplifiedDecal), "Render", typeof(CliffsideWindFlag), typeof(Flagline), typeof(FakeWall));

        HookHelper.SkipMethod(t, nameof(IsSimplifiedParticle),
            typeof(ParticleSystem).GetMethod("Render", new Type[] { }),
            typeof(ParticleSystem).GetMethod("Render", new[] { typeof(float) })
        );
        HookHelper.SkipMethod(t, nameof(IsSimplifiedDistort), "Apply", typeof(Glitch));
        HookHelper.SkipMethod(t, nameof(IsSimplifiedMiniTextbox), "Render", typeof(MiniTextbox));

        IL.Celeste.Distort.Render += DistortOnRender;
        On.Celeste.SolidTiles.ctor += SolidTilesOnCtor;
        On.Celeste.Autotiler.GetTile += AutotilerOnGetTile;
        On.Monocle.Entity.Render += BackgroundTilesOnRender;
        IL.Celeste.BackdropRenderer.Render += BackdropRenderer_Render;
        On.Celeste.DustStyles.Get_Session += DustStyles_Get_Session;

        IL.Celeste.LightningRenderer.Render += LightningRenderer_RenderIL;

        HookHelper.ReturnZeroMethod(t, nameof(SimplifiedWavedBlock),
            typeof(DreamBlock).GetMethodInfo("Lerp"),
            typeof(LavaRect).GetMethodInfo("Wave")
        );
        HookHelper.ReturnZeroMethod(
            t,
            nameof(SimplifiedWavedBlock),
            ModUtils.GetTypes().Where(type => type.FullName?.EndsWith("Renderer+Edge") == true)
                .Select(type => type.GetMethodInfo("GetWaveAt")).ToArray()
        );
        On.Celeste.LightningRenderer.Bolt.Render += BoltOnRender;

        IL.Celeste.Level.Render += LevelOnRender;

        On.Celeste.Audio.Play_string += AudioOnPlay_string;
        HookHelper.SkipMethod(t, nameof(IsSimplifiedLightningStrike), "Render",
            typeof(LightningStrike),
            ModUtils.GetType("ContortHelper", "ContortHelper.BetterLightningStrike")
        );

        HookHelper.SkipMethod(t, nameof(IsSimplifiedClutteredEntity), "Render",
            typeof(ReflectionTentacles), typeof(SummitCloud), typeof(TempleEye), typeof(Wire),
            typeof(Cobweb), typeof(HangingLamp),
            typeof(DustGraphic).GetNestedType("Eyeballs", BindingFlags.NonPublic)
        );
        On.Celeste.FloatingDebris.ctor_Vector2 += FloatingDebris_ctor;
        On.Celeste.MoonCreature.ctor_Vector2 += MoonCreature_ctor;
        On.Celeste.ResortLantern.ctor_Vector2 += ResortLantern_ctor;

        HookHelper.SkipMethod(
            t,
            nameof(IsSimplifiedHud),
            "Render",
            typeof(HeightDisplay), typeof(TalkComponent.TalkComponentUI), typeof(BirdTutorialGui), typeof(CoreMessage), typeof(MemorialText),
            typeof(Player).Assembly.GetType("Celeste.Mod.Entities.CustomHeightDisplay"),
            ModUtils.GetType("Monika's D-Sides", "Celeste.Mod.RubysEntities.AltHeightDisplay")
        );

        On.Celeste.Spikes.ctor_Vector2_int_Directions_string += SpikesOnCtor_Vector2_int_Directions_string;
    }

    [Unload]
    private static void Unload()
    {
        //On.Celeste.Level.Update -= Level_Update;
        //On.Celeste.CrystalStaticSpinner.CreateSprites -= CrystalStaticSpinner_CreateSprites;
        IL.Celeste.CrystalStaticSpinner.GetHue -= CrystalStaticSpinnerOnGetHue;
        IL.Celeste.LightingRenderer.Render -= LightingRenderer_Render;
        On.Celeste.LightningRenderer.Bolt.Render -= BoltOnRender;
        IL.Celeste.Level.Render -= LevelOnRender;
        On.Celeste.ColorGrade.Set_MTexture_MTexture_float -= ColorGradeOnSet_MTexture_MTexture_float;
        IL.Celeste.BloomRenderer.Apply -= BloomRendererOnApply;
        On.Celeste.Decal.Render -= Decal_Render;
        IL.Celeste.Distort.Render -= DistortOnRender;
        On.Celeste.SolidTiles.ctor -= SolidTilesOnCtor;
        On.Celeste.Autotiler.GetTile -= AutotilerOnGetTile;
        On.Monocle.Entity.Render -= BackgroundTilesOnRender;
        IL.Celeste.BackdropRenderer.Render -= BackdropRenderer_Render;
        On.Celeste.DustStyles.Get_Session -= DustStyles_Get_Session;
        IL.Celeste.LightningRenderer.Render -= LightningRenderer_RenderIL;
        On.Celeste.Audio.Play_string -= AudioOnPlay_string;
        On.Celeste.FloatingDebris.ctor_Vector2 -= FloatingDebris_ctor;
        On.Celeste.MoonCreature.ctor_Vector2 -= MoonCreature_ctor;
        On.Celeste.ResortLantern.ctor_Vector2 -= ResortLantern_ctor;
        On.Celeste.Spikes.ctor_Vector2_int_Directions_string -= SpikesOnCtor_Vector2_int_Directions_string;
    }

    private static bool IsSimplifiedGraphics() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool IsSimplifiedParticle() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool IsSimplifiedDistort() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool IsSimplifiedDecal() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool IsSimplifiedMiniTextbox() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool SimplifiedWavedBlock() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static ScreenWipe SimplifiedScreenWipe(ScreenWipe wipe) => null;

    private static bool IsSimplifiedLightningStrike() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool IsSimplifiedClutteredEntity() => CelesteBotMain.Settings.SimplifiedGraphics;

    private static bool IsSimplifiedHud()
    {
        return CelesteBotMain.Settings.SimplifiedGraphics ;
    }

    private static void OnSimplifiedGraphicsChanged(bool simplifiedGraphics)
    {
        if (Engine.Scene is not Level level)
        {
            return;
        }

        if (simplifiedGraphics)
        {
            level.Tracker.GetEntities<FloatingDebris>().ForEach(debris => debris.RemoveSelf());
            level.Entities.FindAll<MoonCreature>().ForEach(creature => creature.RemoveSelf());
        }

        if (simplifiedGraphics  ||
            !simplifiedGraphics && currentSolidTilesStyle != default)
        {
            ReplaceSolidTilesStyle();
        }
    }

    public static void ReplaceSolidTilesStyle()
    {
        if (Engine.Scene is not Level { SolidTiles: { } solidTiles } level)
        {
            return;
        }

        Calc.PushRandom();

        SolidTiles newSolidTiles = new(new Vector2(level.TileBounds.X, level.TileBounds.Y) * 8f, level.SolidsData);

        if (solidTiles.Tiles is { } tiles)
        {
            tiles.RemoveSelf();
            newSolidTiles.Tiles.VisualExtend = tiles.VisualExtend;
            newSolidTiles.Tiles.ClipCamera = tiles.ClipCamera;
        }

        if (solidTiles.AnimatedTiles is { } animatedTiles)
        {
            animatedTiles.RemoveSelf();
            newSolidTiles.AnimatedTiles.ClipCamera = animatedTiles.ClipCamera;
        }

        solidTiles.Add(solidTiles.Tiles = newSolidTiles.Tiles);
        solidTiles.Add(solidTiles.AnimatedTiles = newSolidTiles.AnimatedTiles);

        Calc.PopRandom();
    }

    //private static void Level_Update(On.Celeste.Level.orig_Update orig, Level self)
    //{
    //    orig(self);

    //    // Seems modified the Settings.SimplifiedGraphics property will mess key config.
    //    if (lastSimplifiedGraphics != CelesteBotInteropModule.Settings.SimplifiedGraphics)
    //    {
    //        OnSimplifiedGraphicsChanged(CelesteBotInteropModule.Settings.SimplifiedGraphics);
    //        lastSimplifiedGraphics = CelesteBotInteropModule.Settings.SimplifiedGraphics;
    //    }
    //}

    private static void LightingRenderer_Render(ILContext il)
    {
        ILCursor ilCursor = new(il);
        if (ilCursor.TryGotoNext(
                MoveType.After,
                ins => ins.MatchCall(typeof(MathHelper), "Clamp")
            ))
        {
            ilCursor.EmitDelegate(IsSimplifiedLighting);
        }
    }

    private static float IsSimplifiedLighting(float alpha)
    {
        return CelesteBotMain.Settings.SimplifiedGraphics 
            ? (10 - CelesteBotMain.Settings.SimplifiedLighting.Value) / 10f
            : alpha;
    }

    private static void ColorGradeOnSet_MTexture_MTexture_float(On.Celeste.ColorGrade.orig_Set_MTexture_MTexture_float orig, MTexture fromTex,
        MTexture toTex, float p)
    {
        bool? origEnabled = null;
        if (CelesteBotMain.Settings.SimplifiedGraphics )
        {
            origEnabled = ColorGrade.Enabled;
            ColorGrade.Enabled = false;
        }

        orig(fromTex, toTex, p);
        if (origEnabled.HasValue)
        {
            ColorGrade.Enabled = origEnabled.Value;
        }
    }

    private static void BloomRendererOnApply(ILContext il)
    {
        ILCursor ilCursor = new(il);
        while (ilCursor.TryGotoNext(
                   MoveType.After,
                   ins => ins.OpCode == OpCodes.Ldarg_0,
                   ins => ins.MatchLdfld<BloomRenderer>("Base")
               ))
        {
            ilCursor.EmitDelegate(IsSimplifiedBloomBase);
        }

        while (ilCursor.TryGotoNext(
                   MoveType.After,
                   ins => ins.OpCode == OpCodes.Ldarg_0,
                   ins => ins.MatchLdfld<BloomRenderer>("Strength")
               ))
        {
            ilCursor.EmitDelegate(IsSimplifiedBloomStrength);
        }
    }

    private static float IsSimplifiedBloomBase(float bloomValue)
    {
        return CelesteBotMain.Settings.SimplifiedGraphics 
            ? CelesteBotMain.Settings.SimplifiedBloomBase.Value / 10f
            : bloomValue;
    }

    private static float IsSimplifiedBloomStrength(float bloomValue)
    {
        return CelesteBotMain.Settings.SimplifiedGraphics 
            ? CelesteBotMain.Settings.SimplifiedBloomStrength.Value / 10f
            : bloomValue;
    }

    private static void Decal_Render(On.Celeste.Decal.orig_Render orig, Decal self)
    {
        if (IsSimplifiedDecal())
        {
            string decalName = self.Name.ToLower().Replace("decals/", "");
            if (!SolidDecals.Contains(decalName))
            {
                if (!DecalRegistry.RegisteredDecals.TryGetValue(decalName, out DecalRegistry.DecalInfo decalInfo))
                {
                    return;
                }

                if (decalInfo.CustomProperties.All(pair => pair.Key != "solid"))
                {
                    return;
                }
            }
        }

        orig(self);
    }

    private static void DistortOnRender(ILContext il)
    {
        ILCursor ilCursor = new(il);
        if (ilCursor.TryGotoNext(MoveType.After, i => i.MatchLdsfld(typeof(GFX), "FxDistort")))
        {
            ilCursor.EmitDelegate<Func<Effect, Effect>>(IsSimplifiedDistort);
        }
    }

    private static Effect IsSimplifiedDistort(Effect effect)
    {
        return CelesteBotMain.Settings.SimplifiedGraphics  ? null : effect;
    }

    private static void SolidTilesOnCtor(On.Celeste.SolidTiles.orig_ctor orig, SolidTiles self, Vector2 position, VirtualMap<char> data)
    {
        if (CelesteBotMain.Settings.SimplifiedGraphics && CelesteBotMain.Settings.SimplifiedSolidTilesStyle != default)
        {
            currentSolidTilesStyle = CelesteBotMain.Settings.SimplifiedSolidTilesStyle;
        }
        else
        {
            currentSolidTilesStyle = SolidTilesStyle.All[0];
        }

        creatingSolidTiles = true;
        orig(self, position, data);
        creatingSolidTiles = false;
    }

    private static char AutotilerOnGetTile(On.Celeste.Autotiler.orig_GetTile orig, Autotiler self, VirtualMap<char> mapData, int x, int y,
        Rectangle forceFill, char forceId, Autotiler.Behaviour behaviour)
    {
        char tile = orig(self, mapData, x, y, forceFill, forceId, behaviour);
        if (creatingSolidTiles && CelesteBotMain.Settings.SimplifiedGraphics && CelesteBotMain.Settings.SimplifiedSolidTilesStyle != default && !default(char).Equals(tile) &&
            tile != '0')
        {
            return CelesteBotMain.Settings.SimplifiedSolidTilesStyle.Value;
        }
        else
        {
            return tile;
        }
    }

    private static void ModTileGlitcher(ILCursor ilCursor, ILContext ilContext)
    {
        if (ilCursor.TryGotoNext(ins => ins.OpCode == OpCodes.Callvirt && ins.Operand.ToString().Contains("Monocle.MTexture>::set_Item")))
        {
            if (ilCursor.TryFindPrev(out var cursors, ins => ins.OpCode == OpCodes.Ldarg_0,
                    ins => ins.OpCode == OpCodes.Ldfld && ins.Operand.ToString().Contains("<fgTexes>"),
                    ins => ins.OpCode == OpCodes.Ldarg_0, ins => ins.OpCode == OpCodes.Ldfld,
                    ins => ins.OpCode == OpCodes.Ldarg_0, ins => ins.OpCode == OpCodes.Ldfld
                ))
            {
                for (int i = 0; i < 6; i++)
                {
                    ilCursor.Emit(cursors[0].Next.OpCode, cursors[0].Next.Operand);
                    cursors[0].Index++;
                }

                ilCursor.EmitDelegate(IgnoreNewTileTexture);
            }
        }
    }

    private static MTexture IgnoreNewTileTexture(MTexture newTexture, VirtualMap<MTexture> fgTiles, int x, int y)
    {
        if (CelesteBotMain.Settings.SimplifiedGraphics && CelesteBotMain.Settings.SimplifiedSolidTilesStyle != default)
        {
            if (fgTiles[x, y] is { } texture && newTexture != null)
            {
                return texture;
            }
        }

        return newTexture;
    }

    private static void BackgroundTilesOnRender(On.Monocle.Entity.orig_Render orig, Monocle.Entity self)
    {
        if (self is BackgroundTiles && CelesteBotMain.Settings.SimplifiedGraphics)
        {
            return;
        }

        orig(self);
    }

    private static void BackdropRenderer_Render(ILContext il)
    {
        ILCursor c = new(il);

        Instruction methodStart = c.Next;
        c.EmitDelegate(IsNotSimplifiedBackdrop);
        c.Emit(OpCodes.Brtrue, methodStart);
        c.Emit(OpCodes.Ret);
        if (c.TryGotoNext(ins => ins.MatchLdloc(out int _), ins => ins.MatchLdfld<Backdrop>("Visible")))
        {
            Instruction ldloc = c.Next;
            c.Index += 2;
            c.Emit(ldloc.OpCode, ldloc.Operand).EmitDelegate(IsShow9DBlackBackdrop);
        }
    }

    private static bool IsNotSimplifiedBackdrop()
    {
        return !CelesteBotMain.Settings.SimplifiedGraphics ;
    }

    private static bool IsShow9DBlackBackdrop(bool visible, Backdrop backdrop)
    {
        if (backdrop.Visible && Engine.Scene is Level level)
        {
            bool hideBackdrop =
                backdrop.Name?.StartsWith("bgs/nameguysdsides") == true &&
                (level.Session.Level.StartsWith("g") || level.Session.Level.StartsWith("h")) &&
                level.Session.Level != "hh-08";
            return !hideBackdrop;
        }

        return visible;
    }
    // TODO: Fix
    //private static void CrystalStaticSpinner_CreateSprites(On.Celeste.CrystalStaticSpinner.orig_CreateSprites orig, CrystalStaticSpinner self)
    //{
    //    if (CelesteBotInteropModule.Settings.SimplifiedGraphics && CelesteBotInteropModule.Settings.SimplifiedSpinnerColor.Name >= 0)
    //    {
    //        self.color = CelesteBotInteropModule.Settings.SimplifiedSpinnerColor.Name;
    //    }

    //    orig(self);
    //}

    private static void CrystalStaticSpinnerOnGetHue(ILContext il)
    {
        ILCursor ilCursor = new(il);
        if (ilCursor.TryGotoNext(MoveType.After, ins => ins.MatchCall(typeof(Calc), "HsvToColor")))
        {
            ilCursor.EmitDelegate(IsSimplifiedSpinnerColor);
        }
    }

    private static Color IsSimplifiedSpinnerColor(Color color)
    {
        return CelesteBotMain.Settings.SimplifiedGraphics && CelesteBotMain.Settings.SimplifiedSpinnerColor.Name == CrystalColor.Rainbow ? Color.White : color;
    }

    private static DustStyles.DustStyle DustStyles_Get_Session(On.Celeste.DustStyles.orig_Get_Session orig, Session session)
    {
        if (CelesteBotMain.Settings.SimplifiedGraphics)
        {
            Color color = Color.Transparent;
            return new DustStyles.DustStyle
            {
                EdgeColors = new[] { color.ToVector3(), color.ToVector3(), color.ToVector3() },
                EyeColor = color,
                EyeTextures = "danger/dustcreature/eyes"
            };
        }

        return orig(session);
    }

    private static void FloatingDebris_ctor(On.Celeste.FloatingDebris.orig_ctor_Vector2 orig, FloatingDebris self, Vector2 position)
    {
        orig(self, position);
        if (IsSimplifiedClutteredEntity())
        {
            self.Add(new RemoveSelfComponent());
        }
    }

    private static void MoonCreature_ctor(On.Celeste.MoonCreature.orig_ctor_Vector2 orig, MoonCreature self, Vector2 position)
    {
        orig(self, position);
        if (IsSimplifiedClutteredEntity())
        {
            self.Add(new RemoveSelfComponent());
        }
    }

    private static void ResortLantern_ctor(On.Celeste.ResortLantern.orig_ctor_Vector2 orig, ResortLantern self, Vector2 position)
    {
        orig(self, position);
        if (IsSimplifiedClutteredEntity())
        {
            self.Add(new RemoveSelfComponent());
        }
    }

    private static void LightningRenderer_RenderIL(ILContext il)
    {
        ILCursor c = new(il);
        if (c.TryGotoNext(i => i.MatchLdfld<Entity>("Visible")))
        {
            Instruction lightningIns = c.Prev;
            c.Index++;
            c.Emit(lightningIns.OpCode, lightningIns.Operand).EmitDelegate(IsSimplifiedLightning);
        }

        if (c.TryGotoNext(
                MoveType.After,
                ins => ins.OpCode == OpCodes.Ldarg_0,
                ins => ins.MatchLdfld<LightningRenderer>("DrawEdges")
            ))
        {
            c.EmitDelegate<Func<bool, bool>>(drawEdges => (!CelesteBotMain.Settings.SimplifiedGraphics) && drawEdges);
        }
    }

    private static bool IsSimplifiedLightning(bool visible, Lightning item)
    {
        if (CelesteBotMain.Settings.SimplifiedGraphics)
        {
            Rectangle rectangle = new((int)item.X + 1, (int)item.Y + 1, (int)item.Width, (int)item.Height);
            Draw.SpriteBatch.Draw(GameplayBuffers.Lightning, item.Position + Vector2.One, rectangle, Color.Yellow);
            if (visible)
            {
                Draw.HollowRect(rectangle, Color.LightGoldenrodYellow);
            }

            return false;
        }

        return visible;
    }

    private static void BoltOnRender(On.Celeste.LightningRenderer.Bolt.orig_Render orig, object self)
    {
        if (CelesteBotMain.Settings.SimplifiedGraphics)
        {
            return;
        }

        orig(self);
    }

    private static void LevelOnRender(ILContext il)
    {
        ILCursor ilCursor = new(il);
        if (ilCursor.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchLdfld<Level>("Wipe"), i => i.OpCode == OpCodes.Brfalse_S))
        {
            ilCursor.Index += 2;
            ilCursor.EmitDelegate(SimplifiedScreenWipe);
        }
    }

    private static EventInstance AudioOnPlay_string(On.Celeste.Audio.orig_Play_string orig, string path)
    {
        EventInstance result = orig(path);
        if (CelesteBotMain.Settings.SimplifiedGraphics &&
            path == "event:/new_content/game/10_farewell/lightning_strike")
        {
            result?.setVolume(0);
        }

        return result;
    }

    private static void SpikesOnCtor_Vector2_int_Directions_string(On.Celeste.Spikes.orig_ctor_Vector2_int_Directions_string orig, Spikes self,
        Vector2 position, int size, Spikes.Directions direction, string type)
    {
        if (CelesteBotMain.Settings.SimplifiedGraphics)
        {
            if (self.GetType().FullName != "VivHelper.Entities.AnimatedSpikes")
            {
                type = "outline";
            }
        }

        orig(self, position, size, direction, type);
    }

    private static void ModCustomSpinnerColor(ILContext il)
    {
        ILCursor ilCursor = new(il);
        if (ilCursor.TryGotoNext(MoveType.After,
                i => i.OpCode == OpCodes.Ldarg_0,
                i => i.OpCode == OpCodes.Ldarg_S && i.Operand.ToString() == "tint"
            ))
        {
            ilCursor.EmitDelegate<Func<string, string>>(GetSimplifiedSpinnerColor);
        }
    }

    private static string GetSimplifiedSpinnerColor(string color)
    {
        return CelesteBotMain.Settings.SimplifiedGraphics && CelesteBotMain.Settings.SimplifiedSpinnerColor.Value != null
            ? CelesteBotMain.Settings.SimplifiedSpinnerColor.Value
            : color;
    }

    private static void ModRainbowSpinnerColor(ILCursor ilCursor, ILContext ilContext)
    {
        Instruction start = ilCursor.Next;
        ilCursor.EmitDelegate(IsSimplifiedSpinnerColorNotNull);
        ilCursor.Emit(OpCodes.Brfalse, start);
        ilCursor.EmitDelegate<Func<Color>>(GetSimplifiedSpinnerColor);
        ilCursor.Emit(OpCodes.Ret);
    }

    private static bool IsSimplifiedSpinnerColorNotNull()
    {
        return CelesteBotMain.Settings.SimplifiedGraphics && CelesteBotMain.Settings.SimplifiedSpinnerColor.Value != null;
    }

    private static Color GetSimplifiedSpinnerColor()
    {
        return CelesteBotMain.Settings.SimplifiedSpinnerColor.Color;
    }

    private static void ModVivCustomSpinnerColor(ILContext il)
    {
        ILCursor ilCursor = new(il);
        Instruction start = ilCursor.Next;
        ilCursor.EmitDelegate(IsSimplifiedSpinnerColorNotNull);
        ilCursor.Emit(OpCodes.Brfalse, start);

        Type type = ModUtils.GetType("VivHelper", "VivHelper.Entities.CustomSpinner");
        if (type.GetFieldInfo("color") is { } colorField)
        {
            ilCursor.Emit(OpCodes.Ldarg_0).EmitDelegate<Func<Color>>(GetSimplifiedSpinnerColor);
            ilCursor.Emit(OpCodes.Stfld, colorField);
        }

        if (type.GetFieldInfo("borderColor") is { } borderColorField)
        {
            ilCursor.Emit(OpCodes.Ldarg_0).EmitDelegate(GetTransparentColor);
            ilCursor.Emit(OpCodes.Stfld, borderColorField);
        }
    }

    private static Color GetTransparentColor()
    {
        return Color.Transparent;
    }

    // ReSharper disable FieldCanBeMadeReadOnly.Global
    public record struct SpinnerColor
    {
        public static readonly List<SpinnerColor> All = new() {
            new SpinnerColor((CrystalColor) (-1), null),
            new SpinnerColor(CrystalColor.Rainbow, "#FFFFFF"),
            new SpinnerColor(CrystalColor.Blue, "#639BFF"),
            new SpinnerColor(CrystalColor.Red, "#FF4F4F"),
            new SpinnerColor(CrystalColor.Purple, "#FF4FEF"),
        };

        public CrystalColor Name;
        public string Value;
        public Color Color;

        private SpinnerColor(CrystalColor name, string value)
        {
            Name = name;
            Value = value;
            Color = value == null ? default : Calc.HexToColor(value);
        }

        public override string ToString()
        {
            string result = Name == (CrystalColor)(-1) ? "Default" : Name == CrystalColor.Rainbow ? "White" : Name.ToString();
            return result.ToDialogText();
        }
    }
    internal static string ToDialogText(this string input) => Dialog.Clean("TAS_" + input.Replace(" ", "_"));

    public record struct SolidTilesStyle(string Name, char Value)
    {
        public static readonly List<SolidTilesStyle> All = new() {
            default,
            new SolidTilesStyle("Dirt", '1'),
            new SolidTilesStyle("Snow", '3'),
            new SolidTilesStyle("Girder", '4'),
            new SolidTilesStyle("Tower", '5'),
            new SolidTilesStyle("Stone", '6'),
            new SolidTilesStyle("Cement", '7'),
            new SolidTilesStyle("Rock", '8'),
            new SolidTilesStyle("Wood", '9'),
            new SolidTilesStyle("Wood Stone", 'a'),
            new SolidTilesStyle("Cliffside", 'b'),
            new SolidTilesStyle("Pool Edges", 'c'),
            new SolidTilesStyle("Temple A", 'd'),
            new SolidTilesStyle("Temple B", 'e'),
            new SolidTilesStyle("Cliffside Alt", 'f'),
            new SolidTilesStyle("Reflection", 'g'),
            new SolidTilesStyle("Reflection Alt", 'G'),
            new SolidTilesStyle("Grass", 'h'),
            new SolidTilesStyle("Summit", 'i'),
            new SolidTilesStyle("Summit No Snow", 'j'),
            new SolidTilesStyle("Core", 'k'),
            new SolidTilesStyle("Deadgrass", 'l'),
            new SolidTilesStyle("Lost Levels", 'm'),
            new SolidTilesStyle("Scifi", 'n'),
            new SolidTilesStyle("Template", 'z')
        };

        public string Name = Name;
        public char Value = Value;

        public override string ToString()
        {
            return this == default ? "Default".ToDialogText() : Name;
        }
    }

    // ReSharper restore FieldCanBeMadeReadOnly.Global
}

internal class RemoveSelfComponent : Component
{
    public RemoveSelfComponent() : base(true, false) { }

    public override void Added(Monocle.Entity entity)
    {
        base.Added(entity);
        entity.Visible = false;
        entity.Collidable = false;
        entity.Collider = null;
    }

    public override void Update()
    {
        Entity?.RemoveSelf();
        RemoveSelf();
    }
}