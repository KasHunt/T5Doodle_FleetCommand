> This is not an official Tilt Five product. Read [the disclaimer](#disclaimer) for details.
# FleetCommand for Tilt Five
![Preview Screenshot!](/Recordings/Screenshot.jpg)
</br><sub>Screenshot</sub>

## Description
A turn based naval warfare game for Tilt Five.

You'll need Tilt Five Glasses to run this application. 
If you don't know what this is, visit the [Tilt Five website](https://tiltfive.com)
to discover the future of tabletop AR.  

The third in a number of programming 'doodles' (short, simple, unrefined projects).
This one is substantially larger than the previous two and took me several weekends over a few months.
If I'm honest, I was probably just enjoying making things in Blender.

There are several things that I've left unfinished, or would like to completely replace,
but I've spent too long on this now and it needs to go out before it ends up collecting dust.
I'm not completely happy with the architecture, and it's a little light on comments in some places.
Of the unfinished features, possibly the most egregious is the presence of network button that currently does nothing.
 
PRs/feedback/bugs welcome, though no guarantees that this project will get any
love after it's posted.

## Usage
### Control Panel
All control is done using the control panel at the bottom of the screen. 
The wand is used to interact with physically modelled controls. For controls during play,
press the help button on the control panel or refer to the details below.

#### Thumb Wheel
For the adjustable thumb wheels on the control panel (volume, color, time),
click on either the top or bottom of the wheel to adjust the wheel up and down.

#### Network Button
The networking code is unfinished, but the button is present in the control panel.
At the moment, _the button does nothing_.


### Wand controls
| Control                 | Action                                                                                                                                  |
|-------------------------|-----------------------------------------------------------------------------------------------------------------------------------------|
| Trigger                 | *(Control Panel)* Click to select <br/> *(While placing)* Click and hold to move ships<br/> *(While playing)* Click to fire at a target |
| Thumbstick (Horizontal) | *(While placing)* Rotate ship <br/> *(While playing)* Rotate view                                                                       |
| Thumbstick (Vertical)   | *(While playing)* Zoom in/out                                                                                                           |

## Future Ideas / Known Issues
- [Issue] AI opening moves are a predictable - The AI uses a probability density field approach to choosing it's targets, which in the absence of any hit/miss data always results in the same opening move
- [Issue] Mixcast seems excessively bright
- [Enhancement] Finish implement networking
- [Enhancement] Better team selection
- [Enhancement] More game modes

## Development Time
- For v0.1.0 Approximately 16 days over several months
  - A significant portion of which was the learning curve on creating more complex Blender models.

## Tooling
- Unity 2022.3 **: Game Engine**
- JetBrains Rider 2023.2.1 **: IDE**
- Blender 3.4 **: 3D Model Creation**
- Adobe Photoshop 2024 **: Bitmap Image Editing**
- Adobe Audition 2024 **: Audio Editing**

## Attribution
All third party content is listed in the accompanying NOTICE file.
This is primarily audio under CC0, since I'm not great at creating that. Content not listed in the NOTICE file (including the 3D blender assets) was created by me.

<mark>Whereas my previous doodles were pure CC0 third party assets, this project includes some
awesome CC-BY-4.0 background music by Schwarzenegger Belonio (Migfus20) which merits inclusion.
The font Screaming Neon by Michael Moss is also used under CC-BY-4.0.</mark>

## Disclaimer
This application was personally developed by Kasper John Hunt, who has a
professional association with Tilt Five, the producer of Tilt Five augmented
reality headset on which the application can be run. However, please be advised
that this application is a personal and independent project.

It is not owned, approved, endorsed, or otherwise affiliated with
Tilt Five in any official capacity.

The views, ideas, and content expressed within this application are solely those
of the creator and do not reflect the opinions, policies, or positions of Tilt Five.
Any use of the Tilt Five's name, trademarks, or references to its products is for
descriptive purposes only and does not imply any association or sponsorship by Tilt Five.

Users of this application should be aware that it is provided "as is" without any
warranties or representations of any kind. Any questions, comments, or concerns
related to this application should be directed to Kasper Hunt and not to Tilt Five.

## Copyright
Copyright 2024 Kasper John Hunt

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

## Trademarks and Logos 
The Beachview Studios name and its associated logos are trademarks
of Beachview Studios, and may not be used without explicit written
permission from the trademark owner.

Unless you have explicit, written permission, you may not:

- Reproduce or use the images, logos, or trademarks of Beachview
  Studios in relation to any project that is not directly associated
  with or approved by Beachview Studios.
- Use any name, logo, or trademark of Beachview Studios to endorse
  or promote products or services derived from this software.
