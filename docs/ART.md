> Active Unity world presentation is code-generated geometry in `WorldArt`, `TownArt` and `FrontierArt`, including larger hex plots, fog, clay workings, kilns and bloomeries. The current construction UI uses text cards; the retained PNG illustrations below are historical assets and are not currently displayed.

# Original Town Art

The house, farm and forester illustrations were generated with the built-in imagegen tool on 2026-10-09 for this project. They are transparent PNG construction-card art and references for the procedural 3D silhouettes; they are not 3D models or imported Manor Lords artwork.

## Files and use

| Asset | Source PNG | Runtime use |
| --- | --- | --- |
| House | `Assets/AlphaHex/Resources/TownArt/house.png` | Retained; not currently displayed |
| Farm | `Assets/AlphaHex/Resources/TownArt/farm.png` | Retained; not currently displayed |
| Forester | `Assets/AlphaHex/Resources/TownArt/forester.png` | Retained; not currently displayed |

Originals are 1254 × 1254 RGBA. Unity imports them at a maximum of 256 pixels, with alpha transparency and no mipmaps. The previous `TownHud` loaded them as shared textures. Preserve their `.meta` GUIDs when updating the artwork. No generated art is loaded from a developer's home directory at runtime.

World terrain, buildings, trees, citizens and vehicles remain code-generated meshes/primitives in `WorldArt` and `TownArt`. The cottages use timber framing and pitched roofs; farms use planted rows; foresters use seedling beds. The in-world models are simpler than the illustrations.

## Generation prompts

### House

Use case: stylized-concept. Asset type: original transparent PNG building illustration for the construction panel of Alpha Hex, a hex-island settlement building game, also a reference for simple 3D models. Style: polished hand-painted low-poly miniature, rustic European frontier architecture, warm weathered oak beams, pale warm plaster, muted sage and pine greens, wheat ochre and subdued slate, clean readable silhouette, charming but grounded, generous simplified forms that remain readable at 96 pixels. Composition: one centered building on a very small hexagonal grass-earth plinth, three-quarter isometric view from above, entire object visible with 12% clear margin, square canvas. Soft afternoon light from upper left, gentle contact shadow confined to plinth. Actual transparent background outside the object. No text, no labels, no frame, no people, no logos, no watermarks, no borrowed game designs. Subject: a modest timber-framed pioneer cottage for four citizens, one steep warm thatched gable roof, pale plaster walls with dark oak framing, small stone chimney, wooden front door, two little windows, a stacked firewood pile and a tiny vegetable patch. A single compact dwelling, distinctly residential.

### Farm

Use case: stylized-concept. Asset type: original transparent PNG building illustration for the construction panel of Alpha Hex, a hex-island settlement building game, also a reference for simple 3D models. Style: polished hand-painted low-poly miniature, rustic European frontier architecture, warm weathered oak beams, pale warm plaster, muted sage and pine greens, wheat ochre and subdued slate, clean readable silhouette, charming but grounded, generous simplified forms that remain readable at 96 pixels. Composition: one centered building on a very small hexagonal grass-earth plinth, three-quarter isometric view from above, entire object visible with 12% clear margin, square canvas. Soft afternoon light from upper left, gentle contact shadow confined to plinth. Actual transparent background outside the object. No text, no labels, no frame, no people, no logos, no watermarks, no borrowed game designs. Subject: a compact working farmstead: small timber barn with ochre thatched pitched roof, three orderly short rows of golden wheat and green crops on its small hex plinth, a low split-rail fence, two harvest baskets beside the barn. Single coherent miniature, crops clearly distinguish it from a house.

### Forester

Use case: stylized-concept. Asset type: original transparent PNG building illustration for the construction panel of Alpha Hex, a hex-island settlement building game, also a reference for simple 3D models. Style: polished hand-painted low-poly miniature, rustic European frontier architecture, warm weathered oak beams, pale warm plaster, muted sage and pine greens, wheat ochre and subdued slate, clean readable silhouette, charming but grounded, generous simplified forms that remain readable at 96 pixels. Composition: one centered building on a very small hexagonal grass-earth plinth, three-quarter isometric view from above, entire object visible with 12% clear margin, square canvas. Soft afternoon light from upper left, gentle contact shadow confined to plinth. Actual transparent background outside the object. No text, no labels, no frame, no people, no logos, no watermarks, no borrowed game designs. Subject: a replanting forester's nursery hut: small rustic timber cabin with a muted slate-green pitched roof and open tool lean-to, two seedling beds holding bright young pine saplings, a shovel and watering can, one mature slender pine beside the hut. Emphasize living saplings and woodland restoration rather than cut logs.
