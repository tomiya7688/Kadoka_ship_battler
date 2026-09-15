# Kadoka Ship Battler — Provisional Mixed License

Copyright (c) 2026 tomiya7688.

This repository is currently in a transitional state: the developer tools and the game itself have not yet been fully separated into distinct packages or directories. This document defines the intended licensing rules until that separation is completed.

## 1. Two license categories

The contents of this repository are divided into two categories:

1. **Developer Tool Components** — tools whose primary purpose is to create, generate, edit, convert, validate, or otherwise assist in authoring games or game content.
2. **Game Components** — the Kadoka Ship Battler game itself, including runtime game logic, game-specific data, scenes, characters, artwork, audio, text, story/world material, game-specific prefabs, and other content intended to form part of the distributed game.

Developer Tool Components are licensed under the **MIT License**. See [`LICENSES/DEVELOPER_TOOLS_MIT.txt`](LICENSES/DEVELOPER_TOOLS_MIT.txt).

Game Components are licensed under the **Kadoka Ship Battler Game License (Provisional)**. See [`LICENSES/GAME_LICENSE.md`](LICENSES/GAME_LICENSE.md).

## 2. How a component is classified during the transition

Because the repository has not yet been physically separated, classification is based on purpose and explicit project markings.

A file or component is treated as a Developer Tool Component when at least one of the following applies:

- it is explicitly marked as MIT-licensed;
- it is placed in a directory or package that is explicitly designated as a developer-tool package; or
- project documentation maintained by the copyright holder explicitly identifies it as a developer tool.

Everything else is treated as a Game Component unless the copyright holder states otherwise.

If classification is genuinely unclear, the Game Component license applies until the component is explicitly designated as a Developer Tool Component. This conservative fallback exists only for the transitional period and is not intended to reduce the rights granted to components that are later explicitly designated as developer tools.

## 3. Games and other output created with the Developer Tool Components

You may use the Developer Tool Components to create your own games and other works.

Subject to third-party licenses and the restrictions in Section 4, **output created using the Developer Tool Components may be used, modified, copied, redistributed, sublicensed, and sold, including commercially, under terms of your choice**.

This permission expressly includes games that are structurally or mechanically very similar to Kadoka Ship Battler. For example, a generated game may differ mainly in characters, maps, scenarios, balance, data, or other content and may still be distributed or sold commercially.

There is no requirement that a game created with the Developer Tool Components be open source merely because the tools themselves are MIT-licensed.

If generated output directly contains a copy or substantial portion of MIT-licensed source code from the Developer Tool Components, the MIT copyright and permission notice must be preserved as required by the MIT License. This does not otherwise require the rest of the generated game to use the MIT License.

## 4. What the generated-output permission does not grant

The permissions in Section 3 do **not**, by themselves, grant permission to copy or redistribute Game Components from Kadoka Ship Battler.

In particular, unless separately licensed or explicitly included in generated output with permission, the following remain under the Game Component license or their applicable third-party license:

- Kadoka Ship Battler-specific characters and character assets;
- artwork, animation, audio, music, dialogue, story, lore, maps, scenes, and other creative assets from the game;
- game-specific source code that is not designated as a Developer Tool Component;
- names, logos, branding, or other identifiers to the extent protected by applicable law; and
- third-party packages, assets, or content governed by their own licenses.

You may state truthfully that your work was created with or is compatible with the developer tools, but this license does not grant a right to imply that an independently distributed game is an official Kadoka Ship Battler release or is endorsed by the original project.

## 5. Third-party material

Unity, packages, assets, libraries, fonts, audio, and any other third-party material included in or referenced by this repository remain subject to their respective licenses and terms. This license does not replace or expand rights granted by third parties.

## 6. Future separation

The project intends to physically separate Developer Tool Components from Game Components. After that separation, individual packages or directories may contain their own license files or SPDX identifiers. Those more specific notices take precedence for the files they cover.

## 7. No warranty

Except where prohibited by applicable law, the software and materials are provided "AS IS", without warranty of any kind, express or implied. The copyright holder is not liable for damages arising from use of the software or materials.

---

**Status:** Provisional. This file is intended to express the current licensing policy while the repository is being reorganized. It may be replaced by clearer package-level licensing once the developer tools and the game are separated.