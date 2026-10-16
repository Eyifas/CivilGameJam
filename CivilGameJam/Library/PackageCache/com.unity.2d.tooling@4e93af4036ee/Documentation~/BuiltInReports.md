# Built-in reports
The **Sprite Atlas Analyzer** has six built-in report types which provide specific information about the 2D texture assets contained in all sprite atlases within your project. The report types are:

- All Sprite Atlases
- Atlas page count
- Source textures with compression in Sprite Atlas
- Texture Space Wastage
- Sprite count
- Textures contain different secondary texture count in Sprite Atlas

## All Sprite Atlases
This report shows a detailed breakdown of information on all the sprite atlases that are present in the project. The report is in a table format with the following columns:

Column          | Description
--              | --
**Name**            | The name of the sprite atlas or sprite.
**Sprites**         | The number of sprites in the sprite atlas.
**Total Memory**    | The runtime memory used by the object.
**Unused Memory**   | Unutilized memory usage of the sprite atlas.
**Usage**           | Sprite atlas texture area in percentage that is being used to pack Sprites.
**Total Area**      | Total area of the sprite atlas's texture.
**Used Area**       | Total area of the sprite atlas's texture that is used by to pack Sprites.
**Width**           | The width of the texture for the object.
**Height**          | The height of the texture for the object.
**Page**            | Number of main texture pages the sprite altas has generated.
**Packing Mode**    | Indicate how the sprites were created.
**Texture Format**  | The object's texture format.

## Atlas Page count
Displays the number of sprite atlases that have greater than a set number of pages. A sprite atlas can have multiple pages of sprites. You can set the number of pages you want the report to consider its threshold by selecting this report and setting the value in the **Atlas Texture Pages** right panel.

This information helps to determine how many sprite atlases has multiple pages. If a sprite atlas spans more than two texture pages, sprites used together in a scene could end up on different pages. Unity can only batch sprites that share the same texture page. When sprites are distributed across multiple atlas pages, this causes batch rendering to be unpredictable and may cause performance problems as Unity is unable to batch sprites for rendering efficiently.

## Source textures with compression in sprite atlas
This report lists all source textures that are already compressed before being packed into a sprite atlas, with a column displaying the detected **Texture Format** used in the texture compression. If the source texture is compressed before being packed, users can experience texture quality degradation due to the final texture being compressed twice when packed into a sprite atlas.


## Texture Space Wastage
This report displays the number of sprite atlases that have unused space above a set threshold, in KB. This report measures the amount of unused space in-between packed sprites as wastage. You can set the threshold (in KB) that must be exceeded to be displayed in the report. It's ideal to minimize the amount of wasted texture space in a sprite atlas so that a device's memory is used efficiently.

## Sprite Count
This report lists the number of sprite atlases with sprites less than or equal to the set limit. This allows you to identify which sprite atlases contain a small number of individual sprites and could be packed more efficiently.

## Textures contain different secondary texture count in sprite atlas
This report lists sprite atlases that contains a different number of secondary textures compared to the number of individual sprites. With textures containing different secondary texture count can result in texture space wastage.
