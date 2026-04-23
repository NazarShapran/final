import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '30s', target: 50 }, // ramp up
        { duration: '1m', target: 50 },  // stable
        { duration: '30s', target: 0 },  // ramp down
    ],
};

export default function () {
    const categories = ['Antibiotic', 'Painkiller', 'Vitamin', 'Cardiac', 'Other'];
    const category = categories[Math.floor(Math.random() * categories.length)];
    
    const res = http.get(`http://localhost:5000/api/medicines?category=${category}&inStock=true`);
    check(res, {
        'status is 200': (r) => r.status === 200,
        'response time < 200ms': (r) => r.timings.duration < 200,
    });
    sleep(1);
}
