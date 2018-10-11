These serialisation entities match the sample data and are a fairly bog standard arrangement of non-sealed mutable classes, which makes them easy to test serialisation and deserialisation across different libraries.

The only concession specifically made to make them easier to test with is the addition of the [Serializable] attribute to all classes, which the BinaryFormatter requires.