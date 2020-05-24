# Realtime Volumetric Clouds for Unity 3D
## How to use

This asset contains both a script (and shaders) for cloud generation and, as a subproduct, a script for noise generation.
The cloud shader package **ALSO** contains the script for noise generation, so downloading the both assets is unnecessary!

### How to use the cloud asset: 
1. import the unity package found in https://github.com/todoval/CloudsShader/blob/master/CloudShader.unitypackage to your project
2. create a new 3D Cube object as a container for the clouds
3. turn off the Mesh renderer of the container
4. find the *CloudShader/CloudGenerator.cs* script and add it to a camera (a camera **MUST** be present in the scene)
5. set the following properties of the script:
   - *Container:* set this to your created cube container
   - *Cloud Rendering Shader:* set this to *CloudShader/CloudRendering*
   - *Environment Blending Shader:* set this to *CloudShader/EnvironmentBlending*
6. set other script properties according to your liking
   - beware, properties such as *Cloud Size*, *Ray March Step Size*, *Ray March Step Decrease*, *Absorption Coefficient*, *Density Multiplier* and *Speed* must be set to value other than 0 in order for the cloud shader to work 
7. run the game (however, script will run in edit mode also)

<p align="center">
    <img src="https://github.com/todoval/CloudsShader/blob/master/Screenshots/CloudScript.png" width="300px"</img>  
</p>
<p align="center"> The Cloud Generation Script </p>

### How to use the noise generation asset:
1. import the unity package found in https://github.com/todoval/CloudsShader/blob/master/NoiseGenerator.unitypackage to your project
1. add the *CloudShader/NoiseGenerator/NoiseGenerator.cs* script to any object
2. set the following properties of the script:
   - *Noise Texture Generator:* set this to *Assets/CloudShader/NoiseGenerator/NoiseTextureGenerator.compute*
   - *Slicer:* set this to *Assets/CloudShader/NoiseGenerator/Slicerr.compute*
3. set other script properties according to your liking
4. run the game
5. click individual buttons (Create Shape Texture, Create Detail Texture, Create Weather Map) to create the textures
6. textures will be stored in the *Resources/texture_name* folder

<p align="center">
   <img src="https://github.com/todoval/CloudsShader/blob/master/Screenshots/NoiseGenScript1.png" width="300px" />
   <img src="https://github.com/todoval/CloudsShader/blob/master/Screenshots/NoiseGenScript2.png" width="300px" />
</p>
<p align="center"> The Noise Generation Script </p>


<p align="center">
   <img src="https://github.com/todoval/CloudsShader/blob/master/Screenshots/Resources.png" width="500px" />
</p>
<p align="center"> What the <i>Resources</i> folder should look like after importing either one of the scripts </p>

## Project Results

This section shows a some of the results that can be achieved with this asset. 


Linked down below are also the presentation and a video that was used for the project defense, as this asset was created as a part of a *Computer Graphics for Computer Games course* on MatFyz: 

*Presentation:* https://github.com/todoval/CloudsShader/blob/master/Screenshots/DefensePresentation.pdf
*Video:* https://www.youtube.com/watch?v=K_v5DLWDhNI  

## Future work
Following are the future plans for this project: 
* Directional light support
* Wind direction support
* Performance improvement
   - Temporal reprojection
   - Temporal upsampling improvement

## References
- [Fredrik Haggstrom: Real-time rendering of volumetric clouds](http://www.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=-5880)
- [Rurik Hogfeldt: Convincing Cloud Rendering](https://odr.chalmers.se/handle/20.500.12380/241770)
- [Dean Babić: Volumetric Atmospheric Rendering](https://bib.irb.hr/datoteka/949019.Final_0036470256_56.pdf)
- [Juraj Páleník: Real-time rendering of volumetric clouds](https://is.muni.cz/th/d099f/thesis.pdf)
- [Horizon Zero Dawn: Real-time Volumetric Cloudscapes](https://www.guerrilla-games.com/read/the-real-time-volumetric-cloudscapes-of-horizon-zero-dawn)
- [Sebastian Lague: Coding Adventure - Clouds](https://www.youtube.com/watch?v=4QOcCGI6xOU&t=624s)
