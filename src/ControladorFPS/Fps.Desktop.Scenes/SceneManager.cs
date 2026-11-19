using Fps.Desktop.Physics;

namespace Fps.Desktop.Scenes;

internal sealed class SceneManager(SceneDefinition initialScene)
{
	public SceneDefinition Active { get; private set; } = initialScene;

	public CollisionWorld Collision { get; private set; } = new CollisionWorld(initialScene);

	public void Load(SceneDefinition scene)
	{
		CollisionWorld collision = new CollisionWorld(scene);
		Active = scene;
		Collision = collision;
	}
}
