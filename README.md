# Proland-To-Unity

This is a partial port of [Proland](https://proland.inrialpes.fr/) to Unity. Proland is quite a large and complicate program and I have trimmed this down quite a lot to just its core elements. The core terrain, atmosphere and ocean components have been ported. 

A key part of Proland is its ability to render large planets using some special techniques to calculate the clip space positions in the vertex shaders to minimize precision issues.  This means I have had to do a lot of low level handling of projection and view matrices. This has caused a lot of problems as Unity modifies these matrices behind the scenes to support some platforms and features.

Its made keeping this up to date with the latest version of Unity quite difficult and at the moment it will only work in version 5.5 of Unity. Its also not a good way to use Unity but it made porting this much easier. I might change this in the future to use Unity's default matrices.

The atmosphere is done using the same method in the previous [Brunetons atmospheric scattering](https://www.digital-dust.com/single-post/2017/03/24/Brunetons-atmospheric-scattering-in-Unity) project (one of the Authors of Proland). This means the atmosphere uses precomputed tables that need to be generated. There are some provided but if you want to recreate them I have added a editor window. Go to Windows->Proland->Create Atmosphere Tables to do so.

This project is currently only working in Unity 5.5

See [home page](https://www.digital-dust.com/single-post/2017/08/05/Proland-in-Unity) for more information.

![Proland5](https://static.wixstatic.com/media/1e04d5_854a8254a92c4c73b8047b790b7b03c2~mv2.jpg/v1/fill/w_550,h_550,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_854a8254a92c4c73b8047b790b7b03c2~mv2.jpg)

![Proland4](https://static.wixstatic.com/media/1e04d5_1b5314acc4ae42ebbdaee1453eed307f~mv2.jpg/v1/fill/w_550,h_550,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_1b5314acc4ae42ebbdaee1453eed307f~mv2.jpg)

![Proland3](https://static.wixstatic.com/media/1e04d5_2b91c6ffa99d4a9faa11e5f021cb0b03~mv2.jpg/v1/fill/w_550,h_550,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_2b91c6ffa99d4a9faa11e5f021cb0b03~mv2.jpg)

![Proland2](https://static.wixstatic.com/media/1e04d5_49b07d45e16e47e5b324d77bcd94fb31~mv2.jpg/v1/fill/w_550,h_550,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_49b07d45e16e47e5b324d77bcd94fb31~mv2.jpg)

![Proland1](https://static.wixstatic.com/media/1e04d5_fdfeb72177c04f85a20964aaf9cc9871~mv2.jpg/v1/fill/w_550,h_550,al_c,q_80,usm_0.66_1.00_0.01/1e04d5_fdfeb72177c04f85a20964aaf9cc9871~mv2.jpg)
