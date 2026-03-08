interface Props {
    name: string;
}

function Greeting(props: Props): JSX.Element {
    return <h1>Hello, {props.name}</h1>;
}

export default Greeting;
