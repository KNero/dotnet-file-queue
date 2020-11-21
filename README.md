# Thread safe dotnet file queue

하나의 큐는 큐이름의 폴더 아래로 구조가 생성된다.
```
queue-list
 | --- queue-name1
         | --- error
         |       | Fail_Dequeue_20201120_184307-48846f76-8eb4-439a-97c6-76f2ffc1fc9a
         |       | Fail_Dequeue_20201120_184307-df9e0afe-9e92-we90-sd9e-isewinef9euw
         | 00000000000000000000.queue
         | queue-name1.meta
         
queue-list: 큐가 생성될 부모 디렉토리
queue-name1: 큐이름으로 생성된 큐 디렉토리
error: 에러난 큐 데이터를 에러가 발생시간을 파일이름으로 생성해서 별도로 저장하는 디렉토리(에러파일형식: Fail_Dequeue_yyyyMMdd_HHmmss-guid)
*.queue: 큐데이터가 저장되는 파일
queue-name1.meta: 파일의 읽는 위치와 써야하는 파일 정보를 기록하는 메타파일
```
큐 데이터 파일의 이름은 **config** 의 `MaxQueueSize` 보다 커지면 1씩 증가시키면서 새로 생성한다.

(파일이름 인덱스가 0에서 1로 증가할 경우: 00000000000000000000.queue -> 00000000000000000001.queue)

메타 파일은 24바이트로 이루어져 있으면 **읽는 파일이름 인덱스(8바이트) + 읽는 파일의 읽어야할 위치(8바이트) + 쓰는 파일이름 인덱스(8바이트))**

## Config
```c#
QueueConfig config = new QueueConfig()
{
    QueueDirectory = @"d:\workspace\data\test-queue", // QueueName 의 폴더가 생성될 위치 (Queue폴더가 절대 아님)
    DataConverter = new ObjectSerializer(), // 파일을 읽고 쓸때 byte[] 변환을 해주는 변환기
    QueueName = "test04", // 큐폴더의 이름
    DequeueTimeoutMilliseconds = 5000, // Dequeue 를 기다리는 시간. 기본: -1. ( `<= 0` 경우 무제한 대기) 
    MaxQueueSize = 1024 * 100 // 큐파일의 max 값. 기본: 3221225472. (3GB)
};
```

## 사용하기
```c#
IFileQueue<string> fq = FileQueue<string>.Create(config);
fq.Enqueue(data);
object data = fq.Dequeue();
```

## DataConverter
`IDataConverter`를 구현하여 config 로 전달하면 된다.
```c#
namespace Knero.FileQueue.Converter
{
    public interface IDataConverter
    {
        byte[] Serialize(object o);

        object Deserialize(byte[] data);
    }
}
```
**Serialize**: Enqueue 를 호출하면 데이터를 파일로 쓰기 전에 실행되며 object 를 byte[] 로 변환해 준다.
**Deserialize**: Dequeue 를 호출하면 파일의 데이터를 읽어서 byte[] 를 object 로 변환해 준다.

### 기본적으로 제공되는 Converter
- ObjectSerializer: BinaryFormatter 를 사용하여 변환을 수행한다.
- Utf8Serializer: string 을 Encoding.UTF8 를 사용하여 변환을 수행한다.

## DataBlockParseException 발생
Deserialize 를 수행하는 중 데이터를 파싱하는 과정에서 발생하며 에러가 난 데이터는 `error` 디렉터리 밑으로 단일 파일로 저장된다.

## DequeueTimeoutException 발생
Dequeue 를 호출 후 config 의 DequeueTimeoutMilliseconds가 설정되어 있을 경우 발생하며
발생하는 이유는 Enqueue 가 되지 않아서 데이터가 없거나 장애로 인해 데이터의 일부만을 읽은 경우이다.
발생한 DequeueTimeoutException 의 `IsBroken` 를 사용하면 일부만 읽어진 상태인지 여부를 확인할 수 있고
`QueueData` 를 통해서 현재 읽어진 데이터를 가져올 수 있다.

### DequeueTimeoutException 대처하기
만약 Enqueue 가 없어서 발생했다면 재시도하면 됨으로 간단하지만 일부만 읽었을 경우에는 아래와 같이 대처하는 것이 좋다.
