//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public partial class GameEntity {

    public Varwin.ECS.Components.IdParentComponent idParent { get { return (Varwin.ECS.Components.IdParentComponent)GetComponent(GameComponentsLookup.IdParent); } }
    public bool hasIdParent { get { return HasComponent(GameComponentsLookup.IdParent); } }

    public void AddIdParent(int newValue) {
        var index = GameComponentsLookup.IdParent;
        var component = CreateComponent<Varwin.ECS.Components.IdParentComponent>(index);
        component.Value = newValue;
        AddComponent(index, component);
    }

    public void ReplaceIdParent(int newValue) {
        var index = GameComponentsLookup.IdParent;
        var component = CreateComponent<Varwin.ECS.Components.IdParentComponent>(index);
        component.Value = newValue;
        ReplaceComponent(index, component);
    }

    public void RemoveIdParent() {
        RemoveComponent(GameComponentsLookup.IdParent);
    }
}

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Entitas.CodeGeneration.Plugins.ComponentMatcherApiGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
public sealed partial class GameMatcher {

    static Entitas.IMatcher<GameEntity> _matcherIdParent;

    public static Entitas.IMatcher<GameEntity> IdParent {
        get {
            if (_matcherIdParent == null) {
                var matcher = (Entitas.Matcher<GameEntity>)Entitas.Matcher<GameEntity>.AllOf(GameComponentsLookup.IdParent);
                matcher.componentNames = GameComponentsLookup.componentNames;
                _matcherIdParent = matcher;
            }

            return _matcherIdParent;
        }
    }
}
