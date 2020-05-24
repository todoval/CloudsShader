# Realtime Volumetric Clouds for Unity 3D
## How to use

This asset contains both a script (and shaders) for cloud generation and, as a subproduct, a script for noise generation. 

### How to use the cloud asset: 
1. import the unity package to your project
2. create a new 3D Cube object as a container for the clouds
3. turn off the Mesh renderer of the container
4. find the *CloudShader/CloudGenerator.cs* script and add it to a camera (a camera MUST be present in the scene)
5. set the following properties of the script:
   - *Container:* set this to your created cube container
   - *Cloud Rendering Shader:* set this to *CloudShader/CloudRendering*
   - *Environment Blending Shader:* set this to *CloudShader/EnvironmentBlending*
6. set other script properties according to your liking
7. run the game (however, script will run in edit mode also)

### How to use the noise generation asset:
1. add the CloudShader/NoiseGenerator/NoiseGenerator.cs script to any object
2. 

## Project Results


## References
- [Fredrik Haggstrom: Real-time rendering of volumetric clouds](http://www.diva-portal.org/smash/record.jsf?pid=diva2%3A1223894&dswid=-5880)
- [Rurik Hogfeldt: Convincing Cloud Rendering](https://odr.chalmers.se/handle/20.500.12380/241770)
- [Dean Babić: Volumetric Atmospheric Rendering](https://bib.irb.hr/datoteka/949019.Final_0036470256_56.pdf)
- [Juraj Páleník: Real-time rendering of volumetric clouds](https://is.muni.cz/th/d099f/thesis.pdf)
- [Horizon Zero Dawn: Real-time Volumetric Cloudscapes](https://www.guerrilla-games.com/read/the-real-time-volumetric-cloudscapes-of-horizon-zero-dawn)
- [Sebastian Lague: Coding Adventure - Clouds](https://www.youtube.com/watch?v=4QOcCGI6xOU&t=624s)
