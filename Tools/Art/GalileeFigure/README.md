# Galilean costume fitting

The five playable and speaking figures share their existing humanoid rigs and expressive faces. Each garment has its own surface fitted to the front, back and lateral studies. Bind-pose corrections adapt the photographed arm and leg proportions to each actor. The independent lateral costume study supplies the fabric behind the arm. The color coordinates stay on the cloth throughout animation.

## Rebuild the source meshes

Run from the repository root with Python 3.11 or newer, NumPy, SciPy, Pillow and OpenCV. In the following commands, `$fit` is an empty local working directory. Both pinned measurements and all six original costume studies are included.

```sh
unzip -q Tools/Art/GalileeAtelier/fitted-cast.zip -d "$fit"
unzip -q Tools/Art/GalileeFigure/costume-reference.zip -d "$fit"
python Tools/Art/GalileeFigure/observations.py "$fit/turnarounds" "$fit/observations"
python Tools/Art/GalileeFigure/export_figure.py "$fit" "$fit/fitted"
cp "$fit"/fitted/*-figure.jpg Games/PathOfDawn/Assets/Art/GalileeFigure/
```

Set `FIRSTGAME_FIGURE_INPUT` to the absolute working directory. In the PathOfDawn Unity 6000.3.26f1 project, execute `FirstGame.Editor.GospelFigureFitting.Bake`. The importer binds the exported geometry and poses, configures the material, and saves the existing five character prefabs. The committed Unity assets are ready for the player build.

To remeasure a changed wardrobe study, supply the pinned Depth Anything V2 Small ONNX file through `observations.py --depth-model`, then run `pose_points.py "$fit/observations" path/to/pose.onnx`. This optional step uses ONNX Runtime. After changing the underlying face or rig, `FirstGame.Editor.GospelFigureStudio.CaptureRest` exports the new head and neck landmarks.

Set `FIRSTGAME_FIGURE_STUDIO` to an output directory and execute `FirstGame.Editor.GospelFigureStudio.Capture` to photograph the evaluated idle and walking poses from fixed cameras. Reuse the same cameras and lighting when comparing two builds.
