import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    scenarios: {
        stress: {
            executor: 'ramping-arrival-rate',
            preAllocatedVUs: 100,
            timeUnit: '1s',
            stages: [
                { duration: '1m', target: 10 }, // 10 iterations per second
                { duration: '2m', target: 20 },
                { duration: '1m', target: 0 },
            ],
        },
    },
};

export default function () {
    // In a real scenario, we would fetch existing medicine IDs.
    // For this test, we assume we fetch it once or randomly guess UUIDs,
    // but the API supports fetching first. Let's do a simple get to find an ID, then post.

    let medRes = http.get('http://localhost:5000/api/medicines?inStock=true');
    let medicines = [];
    if (medRes.status === 200) {
        medicines = JSON.parse(medRes.body);
    }
    
    if (medicines.length === 0) return;

    // Pick a random medicine that doesn't require prescription to avoid needing it
    let availableMeds = medicines.filter(m => !m.requiresPrescription);
    if(availableMeds.length === 0) return;
    
    let med = availableMeds[Math.floor(Math.random() * availableMeds.length)];

    const payload = JSON.stringify({
        prescriptionId: null,
        items: [
            {
                medicineId: med.id,
                quantity: 1
            }
        ]
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const res = http.post('http://localhost:5000/api/sales', payload, params);

    check(res, {
        'sale created successfully (200)': (r) => r.status === 200,
    });
    
    sleep(1);
}
