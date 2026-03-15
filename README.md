# VPE - Abyssweaver

Vanilla Psycasts Expanded custom path mod for RimWorld 1.6.

## 中文简介

`Abyssweaver（渊织者）` 是一个围绕异象、血肉畸变与虚空召唤的 VPE 流派模组。  
包含多阶段召唤、范围控制、腐化与恶变、以及终极实体【渊诞神孽】。

## English Summary

`Abyssweaver` is a VPE custom path focused on anomaly-themed mutation, corruption, summoning, and battlefield control, including the permanent apex summon `Abyssal Atrocity`.

## Dependencies

- RimWorld 1.6
- Anomaly DLC
- Harmony (`brrainz.harmony`)
- Vanilla Expanded Framework
- Vanilla Psycasts Expanded

## Package ID

- Fixed package id: `wuxt.vpe.abyssweaver`
- Do not change this after release unless you intentionally publish a different mod.

## Install

1. Put this folder into your RimWorld `Mods` directory.
2. Enable dependencies first.
3. Enable `VPE - Abyssweaver`.
4. Ensure load order is after VEF and VPE.

## Build (Source)

1. Open `Source/VPE-Abyssweaver/VPE-Abyssweaver.csproj`.
2. Adjust local path properties in the project file if needed.
3. Build with `Release` configuration.
4. Output assembly is copied to `1.6/Core/Assemblies`.

## Localization

- Active language assets are in `1.6/Core/Languages`.
- Chinese and English are both provided via DefInjected + Keyed entries.

## Save Compatibility

- Legacy compatibility defs/path keys were removed for a clean first release.
- Recommended: start a new save when testing this release branch.

## Development Notes

- Main defs: `1.6/Core/Defs`
- Main code: `Source/VPE-Abyssweaver`
- Main textures: `1.6/Core/Textures`
