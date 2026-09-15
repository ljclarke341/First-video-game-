# UI assets

This folder is intentionally empty.

Every sprite in the game — rounded panels, circles, rings, the garage backdrop and the car
silhouette — is **drawn in code at runtime** by `Assets/Scripts/Unity/UI/UISprites.cs`, and every
colour and size comes from `Theme.cs`.

That means there are no textures to import, no import settings to get wrong, and no art pipeline to
set up before the game will run.

If you want to bring in real artwork later, this is where it goes. The swap is small: `UISprites`
is the only place that creates sprites, so pointing those methods at imported assets changes the
look of the entire game.
