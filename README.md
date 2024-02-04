Unfortunately Unity insists on not extending their `CharacterController`, which is only capable of moving the character in a single plane. 
The project includes a player controller that has physics properties (extends the `RigidBody`). The controller comes with features such as 
climbing and slope walking. It takes an up vector, updating the front and right vectors accordingly at each frame. It also uses some animations from [Mixamo](https://www.mixamo.com/).
