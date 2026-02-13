```mermaid
flowchart LR
    System_IServiceProvider["IServiceProvider"]
    Qudi_Examples_RegistrationOrder_RegistrationOrderExecutor["RegistrationOrderExecutor"]
    Qudi_Examples_RegistrationOrder_RegistrationOrderExecutor --> System_IServiceProvider
    classDef cls fill:#bbdefb,stroke:#2196f3,stroke-width:2px,color:#000;
    class Qudi_Examples_RegistrationOrder_RegistrationOrderExecutor cls;
    classDef external fill:#ffe0b2,stroke:#ff9800,stroke-width:1px,stroke-dasharray:3 3,color:#e65100;
    class System_IServiceProvider external;

```