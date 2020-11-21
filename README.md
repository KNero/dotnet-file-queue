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

메타 파일은 24바이트로 이루어져 있으면 **읽는 파일이름 인덱스(8바이트) + 읽는 파일의 읽어야할 위치(8바이트) + 이어서 써야 하는 파일이름 인덱스(8바이트))**

### Config
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

### 사용하기
```c#
IFileQueue<string> fq = FileQueue<string>.Create(config);
fq.Enqueue(data);
object data = fq.Dequeue();
```

### DataConverter

### DataBlockParseException 발생

### DequeueTimeoutException 발생
